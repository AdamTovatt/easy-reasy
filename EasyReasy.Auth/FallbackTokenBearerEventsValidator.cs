using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Fails the bearer options when the token fallback that <see cref="FallbackTokenBearerEvents"/> installs no
    /// longer reaches the handler: <see cref="AuthenticationSchemeOptions.EventsType"/> set anywhere, or
    /// <see cref="JwtBearerOptions.Events"/> replaced by a consumer's <c>PostConfigure</c> registered after
    /// <see cref="AuthServiceCollectionExtensions.AddEasyReasyAuth(Microsoft.Extensions.DependencyInjection.IServiceCollection, string)"/>.
    /// Registered with <c>ValidateOnStart</c>, so the app stops at startup instead of silently dropping the fallback.
    /// </summary>
    /// <remarks>
    /// Options validation runs after every <c>Configure</c> and <c>PostConfigure</c>, so it sees the final options.
    /// The check is on the type, not on one instance, because the options are rebuilt per build. That also rejects
    /// a consumer's own subclass that forwards to the wrapper, since the library cannot verify what it forwards.
    /// </remarks>
    internal sealed class FallbackTokenBearerEventsValidator : IValidateOptions<JwtBearerOptions>
    {
        private readonly IReadOnlyList<IBearerTokenSource> _sources;

        public FallbackTokenBearerEventsValidator(IReadOnlyList<IBearerTokenSource> sources)
        {
            _sources = sources;
        }

        /// <inheritdoc />
        public ValidateOptionsResult Validate(string? name, JwtBearerOptions options)
        {
            // The fallback is installed on the default bearer scheme only; another scheme's events are its own.
            if (name != JwtBearerDefaults.AuthenticationScheme)
            {
                return ValidateOptionsResult.Skip;
            }

            string optionNames = string.Join(" and ", _sources.Select(source => source.OptionName));
            string eventsOption = $"{nameof(JwtBearerOptions)}.{nameof(JwtBearerOptions.Events)}";

            if (options.EventsType != null)
            {
                return ValidateOptionsResult.Fail(
                    $"{optionNames} cannot be combined with {nameof(JwtBearerOptions)}.{nameof(JwtBearerOptions.EventsType)}, " +
                    $"because the bearer handler then ignores {eventsOption}, where the token fallback is installed. " +
                    $"Assign {eventsOption} in Configure instead; the fallback wraps it.");
            }

            if (options.Events is not FallbackTokenBearerEvents)
            {
                return ValidateOptionsResult.Fail(
                    $"{optionNames} needs the token fallback that {nameof(AuthServiceCollectionExtensions.AddEasyReasyAuth)} installs as {eventsOption}, " +
                    $"but a PostConfigure registered after {nameof(AuthServiceCollectionExtensions.AddEasyReasyAuth)} replaced it with {options.Events?.GetType().FullName ?? "null"}. " +
                    $"Add handlers to the existing {eventsOption} there, or assign {eventsOption} in Configure; the fallback wraps it.");
            }

            return ValidateOptionsResult.Success;
        }
    }
}
