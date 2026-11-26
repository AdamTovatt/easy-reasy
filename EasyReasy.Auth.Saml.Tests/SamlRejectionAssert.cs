namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// Asserts which layer refused a response: ITfoxtec, whose exception the refusal carries, or this package's own
    /// checks, which carry none. The layer is what vetting the library needs to know, so each hostile case names it.
    /// </summary>
    internal static class SamlRejectionAssert
    {
        /// <summary>
        /// Asserts the library refused the response with an exception of type <typeparamref name="TException"/>.
        /// </summary>
        public static void ByLibrary<TException>(SamlResponseRejectedException rejection, string? messagePart = null)
            where TException : Exception
        {
            Assert.IsInstanceOfType(rejection.InnerException, typeof(TException), rejection.ToString());
            if (messagePart != null)
            {
                StringAssert.Contains(rejection.InnerException!.Message, messagePart, rejection.ToString());
            }
        }

        /// <summary>
        /// Asserts this package's own check refused a response the library accepted, with a message naming
        /// <paramref name="messagePart"/>.
        /// </summary>
        public static void ByPackage(SamlResponseRejectedException rejection, string messagePart)
        {
            Assert.IsNull(rejection.InnerException, rejection.ToString());
            StringAssert.Contains(rejection.Message, messagePart);
        }
    }
}
