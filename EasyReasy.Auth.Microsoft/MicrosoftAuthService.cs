using EasyReasy.Auth.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The registered <see cref="IMicrosoftAuthService"/>: the shared OpenID Connect code flow with the app registration
    /// as a provider per tenant segment, and the Entra rules on top: work and school accounts only, the flow's tenant,
    /// the allowed tenants, an authentication context through <c>acrs</c>, and the <c>tid</c> plus <c>oid</c> identity.
    /// </summary>
    internal sealed class MicrosoftAuthService : IMicrosoftAuthService
    {
        private readonly OpenIdConnectCodeFlow _flow;
        private readonly MicrosoftProviderResolver _resolver;
        private readonly IReadOnlySet<Guid> _allowedTenantIds;

        /// <summary>
        /// Initializes a new instance of the <see cref="MicrosoftAuthService"/> class.
        /// </summary>
        /// <param name="flow">The package's own flow, under its own state key.</param>
        /// <param name="resolver">The app registration as a provider per tenant segment.</param>
        /// <param name="allowedTenantIds">The tenants whose accounts are accepted, or empty for any work or school
        /// tenant.</param>
        public MicrosoftAuthService(OpenIdConnectCodeFlow flow, MicrosoftProviderResolver resolver, IReadOnlySet<Guid> allowedTenantIds)
        {
            _flow = flow;
            _resolver = resolver;
            _allowedTenantIds = allowedTenantIds;
        }

        /// <inheritdoc />
        public async Task<OpenIdConnectAuthBeginResult> BeginAsync(MicrosoftAuthBeginRequest request, HttpContext httpContext, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(httpContext);
            ValidateEntraRules(request);

            OpenIdConnectAuthBeginRequest flowRequest = new OpenIdConnectAuthBeginRequest
            {
                ProviderKey = MicrosoftAuthority.GetTenantSegment(request.TenantId),
                RedirectUri = request.RedirectUri,
                CallerBinding = request.CallerBinding,
                Prompt = request.Prompt,
                Nonce = request.Nonce,
                LoginHint = request.LoginHint,
                MaxAuthenticationAge = request.MaxAuthenticationAge,
                RequiredClaimValues = request.RequiredAuthenticationContextId is string contextId
                    ? new Dictionary<string, string> { [MicrosoftAuthority.AuthenticationContextClaim] = contextId }
                    : null,
            };

            return await _flow.BeginAsync(flowRequest, _resolver, httpContext, cancellationToken)
                ?? throw new UnreachableException("Every tenant segment resolves to a provider.");
        }

        /// <inheritdoc />
        public async Task<MicrosoftAuthCompletionResult> CompleteAsync(OpenIdConnectAuthorizationResponse response, string expectedCallerBinding, HttpContext httpContext, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(response);
            ArgumentNullException.ThrowIfNull(expectedCallerBinding);
            ArgumentNullException.ThrowIfNull(httpContext);

            OpenIdConnectCodeFlowResult result = await _flow.CompleteAsync(response, expectedCallerBinding, _resolver, httpContext, cancellationToken);
            if (result.UserInfo == null)
            {
                // The flow sets the identity whenever the token validated, so a result without one is a failure.
                return MicrosoftAuthCompletionResult.Failed(
                    result.FailureReason ?? throw new UnreachableException("A result without an identity has a reason."));
            }

            MicrosoftUserInfo? userInfo = ReadIdentity(result.UserInfo);
            if (userInfo == null)
            {
                return MicrosoftAuthCompletionResult.Failed(OpenIdConnectAuthFailureReason.InvalidToken);
            }

            // The tenant rules first, so a refused account is reported for its tenant before the flow's other rules.
            MicrosoftAuthFailureReason? tenantRefusal = CheckTenantRules(userInfo, MicrosoftAuthority.GetRequiredTenantId(result.UserInfo.ProviderKey));
            if (tenantRefusal is MicrosoftAuthFailureReason tenantReason)
            {
                return MicrosoftAuthCompletionResult.Refused(tenantReason, userInfo);
            }

            if (!result.Success)
            {
                // The authentication context is the one required claim this package asks for, so a refusal of it is
                // the context's own Entra reason; every other rule's refusal passes through as the flow gave it.
                return result.FailureReason is OpenIdConnectAuthFailureReason.MissingRequiredClaim or OpenIdConnectAuthFailureReason.RequiredClaimMismatch
                    ? MicrosoftAuthCompletionResult.Refused(MicrosoftAuthFailureReason.MissingAuthenticationContext, userInfo)
                    : MicrosoftAuthCompletionResult.Refused(result.FailureReason.Value, userInfo);
            }

            return MicrosoftAuthCompletionResult.Succeeded(userInfo, result.IdToken, result.IdTokenSigningKey);
        }

        /// <summary>
        /// The rules for a genuine account's tenant: no personal account, the flow's tenant, and the allowed tenants.
        /// </summary>
        private MicrosoftAuthFailureReason? CheckTenantRules(MicrosoftUserInfo userInfo, Guid? requiredTenantId)
        {
            if (userInfo.TenantId == MicrosoftAuthority.PersonalAccountsTenantId)
            {
                return MicrosoftAuthFailureReason.PersonalAccount;
            }

            if (requiredTenantId is Guid required && userInfo.TenantId != required)
            {
                return MicrosoftAuthFailureReason.TenantMismatch;
            }

            if (_allowedTenantIds.Count > 0 && !_allowedTenantIds.Contains(userInfo.TenantId))
            {
                return MicrosoftAuthFailureReason.TenantNotAllowed;
            }

            return null;
        }

        /// <summary>
        /// The Entra identity of a validated token: a GUID <c>tid</c> and a non-blank <c>oid</c>, or null when either is
        /// missing or malformed.
        /// </summary>
        private static MicrosoftUserInfo? ReadIdentity(OpenIdConnectUserInfo token)
        {
            if (!Guid.TryParse(FindClaim(token, MicrosoftAuthority.TenantClaim), out Guid tenantId))
            {
                return null;
            }

            string? objectId = FindClaim(token, MicrosoftAuthority.ObjectIdClaim);
            if (string.IsNullOrWhiteSpace(objectId))
            {
                return null;
            }

            return new MicrosoftUserInfo
            {
                TenantId = tenantId,
                ObjectId = objectId,
                AuthenticationTime = token.AuthenticationTime,
                Nonce = token.Nonce,
                PreferredUsername = FindClaim(token, MicrosoftAuthority.PreferredUsernameClaim),
                Name = token.Name,
                AuthenticationMethods = token.AuthenticationMethods,
                AuthenticationContextIds = token.Claims.Where(claim => claim.Type == MicrosoftAuthority.AuthenticationContextClaim).Select(claim => claim.Value).ToList(),
            };
        }

        private static string? FindClaim(OpenIdConnectUserInfo token, string type)
        {
            return token.Claims.FirstOrDefault(claim => claim.Type == type)?.Value;
        }

        private static void ValidateEntraRules(MicrosoftAuthBeginRequest request)
        {
            List<string> errors = new List<string>();

            if (request.TenantId == MicrosoftAuthority.PersonalAccountsTenantId)
            {
                errors.Add("The tenant id is the personal Microsoft accounts tenant, whose accounts are always refused.");
            }

            if (request.RequiredAuthenticationContextId != null && string.IsNullOrWhiteSpace(request.RequiredAuthenticationContextId))
            {
                errors.Add($"{nameof(MicrosoftAuthBeginRequest.RequiredAuthenticationContextId)} must be null or non-blank.");
            }

            if (errors.Count > 0)
            {
                throw new ArgumentException($"Invalid {nameof(MicrosoftAuthBeginRequest)}:\n{string.Join("\n", errors)}", nameof(request));
            }
        }
    }
}
