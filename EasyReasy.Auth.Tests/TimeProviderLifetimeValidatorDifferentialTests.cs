using Microsoft.IdentityModel.Tokens;
using System.Reflection;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Pins <see cref="TimeProviderLifetimeValidator"/> to IdentityModel's own <see cref="Validators.ValidateLifetime(DateTime?, DateTime?, SecurityToken, TokenValidationParameters)"/>:
    /// every case in a grid of not-before, expiry, clock skew and flag values must be accepted or rejected by both,
    /// with the same exception type.
    /// </summary>
    [TestClass]
    public class TimeProviderLifetimeValidatorDifferentialTests
    {
        private static readonly TimeSpan[] ClockSkews = [TimeSpan.Zero, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5)];

        [TestMethod]
        public void Validate_GridOnSharedFakeClock_MatchesIdentityModelExactlyIncludingBoundaries()
        {
            // IdentityModel reads a clock that TokenValidationParameters keeps non-public. Setting it here lets both
            // validators see the same instant, so cases can sit exactly on a boundary, one tick inside or one tick out.
            // Each way IdentityModel could drift fails here, naming the member, rather than letting the grid run
            // against the system clock.
            const string Member = "TokenValidationParameters.TimeProvider (non-public)";
            PropertyInfo? identityModelClock = typeof(TokenValidationParameters).GetProperty("TimeProvider", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(identityModelClock, $"IdentityModel no longer has {Member}, which this oracle sets. If it is now public, use it instead of the mirrored validator.");
            Assert.AreEqual(typeof(TimeProvider), identityModelClock.PropertyType, $"{Member} is no longer a TimeProvider.");
            Assert.IsNotNull(identityModelClock.SetMethod, $"{Member} can no longer be set.");
            AssertIdentityModelReadsClock(identityModelClock, Member);

            // Clocks near either end of DateTime too, where adding the skew would overflow without saturation.
            DateTime[] clockInstants =
            [
                new DateTime(2030, 6, 15, 12, 0, 0, DateTimeKind.Utc),
                DateTime.SpecifyKind(DateTime.MinValue.AddSeconds(1), DateTimeKind.Utc),
                DateTime.SpecifyKind(DateTime.MaxValue.AddSeconds(-1), DateTimeKind.Utc),
            ];
            int compared = 0;

            foreach (DateTime utcNow in clockInstants)
            {
                FakeTimeProvider clock = new FakeTimeProvider(new DateTimeOffset(utcNow));
                compared += CompareGrid(identityModelClock, clock, utcNow);
            }

            // Guards the grid itself: a filter that silently thinned it would otherwise pass. Clocks x skews x
            // nbf instants x exp instants x RequireExpirationTime x ValidateLifetime.
            Assert.AreEqual(clockInstants.Length * ClockSkews.Length * 12 * 12 * 2 * 2, compared);
        }

        /// <summary>
        /// Fails unless IdentityModel's lifetime check reads the property: a token that the system clock accepts must
        /// be rejected once the property holds a clock past its expiry.
        /// </summary>
        private static void AssertIdentityModelReadsClock(PropertyInfo identityModelClock, string member)
        {
            DateTime utcNow = DateTime.UtcNow;
            TokenValidationParameters parameters = new TokenValidationParameters { ClockSkew = TimeSpan.Zero };
            identityModelClock.SetValue(parameters, new FakeTimeProvider(new DateTimeOffset(utcNow.AddDays(10))));

            string outcome = Outcome(() => Validators.ValidateLifetime(utcNow.AddMinutes(-1), utcNow.AddDays(1), null, parameters), includeMessage: false);

            StringAssert.StartsWith(outcome, nameof(SecurityTokenExpiredException), $"IdentityModel's lifetime check no longer reads {member}, so the grid would compare against the system clock.");
        }

        private static int CompareGrid(PropertyInfo identityModelClock, FakeTimeProvider clock, DateTime utcNow)
        {
            int compared = 0;

            foreach (TimeSpan skew in ClockSkews)
            {
                DateTime?[] instants = BoundaryInstants(utcNow, skew);

                foreach (DateTime? notBefore in instants)
                {
                    foreach (DateTime? expires in instants)
                    {
                        foreach (bool requireExpirationTime in new[] { true, false })
                        {
                            foreach (bool validateLifetime in new[] { true, false })
                            {
                                TokenValidationParameters parameters = new TokenValidationParameters
                                {
                                    ClockSkew = skew,
                                    RequireExpirationTime = requireExpirationTime,
                                    ValidateLifetime = validateLifetime,
                                };
                                identityModelClock.SetValue(parameters, clock);

                                string expected = Outcome(() => Validators.ValidateLifetime(notBefore, expires, null, parameters), includeMessage: true);
                                string actual = Outcome(() => TimeProviderLifetimeValidator.Create(clock)(notBefore, expires, null, parameters), includeMessage: true);

                                Assert.AreEqual(
                                    expected,
                                    actual,
                                    $"nbf={Describe(notBefore, utcNow)}, exp={Describe(expires, utcNow)}, skew={skew}, requireExp={requireExpirationTime}, validateLifetime={validateLifetime}");
                                compared++;
                            }
                        }
                    }
                }
            }

            return compared;
        }

        [TestMethod]
        public void Validate_GridOnSystemClock_MatchesIdentityModel()
        {
            // Offsets stay a minute clear of every boundary, so the instants between the two calls cannot matter.
            DateTime utcNow = DateTime.UtcNow;
            TimeSpan[] offsets = [TimeSpan.FromDays(-1), TimeSpan.FromMinutes(-7), TimeSpan.FromMinutes(-2), TimeSpan.FromMinutes(-1), TimeSpan.Zero, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(7), TimeSpan.FromDays(1)];
            List<DateTime?> instants = [null, .. offsets.Select(offset => (DateTime?)(utcNow + offset))];
            int compared = 0;

            foreach (TimeSpan skew in ClockSkews)
            {
                foreach (DateTime? notBefore in instants)
                {
                    foreach (DateTime? expires in instants)
                    {
                        TokenValidationParameters parameters = new TokenValidationParameters { ClockSkew = skew };

                        // Messages carry the current time to the second, which can tick between the two calls.
                        string expected = Outcome(() => Validators.ValidateLifetime(notBefore, expires, null, parameters), includeMessage: false);
                        string actual = Outcome(() => TimeProviderLifetimeValidator.Create(TimeProvider.System)(notBefore, expires, null, parameters), includeMessage: false);

                        Assert.AreEqual(expected, actual, $"nbf={Describe(notBefore, utcNow)}, exp={Describe(expires, utcNow)}, skew={skew}");
                        compared++;
                    }
                }
            }

            Assert.AreEqual(ClockSkews.Length * instants.Count * instants.Count, compared);
        }

        private static DateTime?[] BoundaryInstants(DateTime utcNow, TimeSpan skew)
        {
            TimeSpan tick = TimeSpan.FromTicks(1);
            DateTime earliestAccepted = Saturate(utcNow, -skew);
            DateTime latestAccepted = Saturate(utcNow, skew);

            return
            [
                null,
                DateTime.MinValue,
                Saturate(utcNow, TimeSpan.FromDays(-1)),
                Saturate(earliestAccepted, -tick),
                earliestAccepted,
                Saturate(earliestAccepted, tick),
                utcNow,
                Saturate(latestAccepted, -tick),
                latestAccepted,
                Saturate(latestAccepted, tick),
                Saturate(utcNow, TimeSpan.FromDays(1)),
                DateTime.MaxValue,
            ];
        }

        // Only picks the grid's instants; the validator under test saturates on its own.
        private static DateTime Saturate(DateTime time, TimeSpan offset)
        {
            decimal ticks = Math.Clamp((decimal)time.Ticks + offset.Ticks, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks);
            return new DateTime((long)ticks, DateTimeKind.Utc);
        }

        /// <summary>
        /// Everything a caller can observe about a validation: the exception type, its message (which carries the IDX
        /// code consumers match on), and the NotBefore/Expires properties JwtBearer renders into its challenge.
        /// </summary>
        private static string Outcome(Action validate, bool includeMessage)
        {
            try
            {
                validate();
                return "accepted";
            }
            catch (Exception exception)
            {
                string properties = exception switch
                {
                    SecurityTokenInvalidLifetimeException invalid => $" nbf={invalid.NotBefore:O} exp={invalid.Expires:O}",
                    SecurityTokenNotYetValidException notYetValid => $" nbf={notYetValid.NotBefore:O}",
                    SecurityTokenExpiredException expired => $" exp={expired.Expires:O}",
                    _ => "",
                };

                return $"{exception.GetType().Name}{properties}{(includeMessage ? $" | {exception.Message}" : "")}";
            }
        }

        private static string Describe(DateTime? instant, DateTime utcNow)
        {
            if (instant == null)
            {
                return "none";
            }

            if (instant == DateTime.MinValue || instant == DateTime.MaxValue)
            {
                return instant.Value.ToString("O");
            }

            return $"now{(instant.Value - utcNow).Ticks:+0;-0;+0}ticks";
        }
    }
}
