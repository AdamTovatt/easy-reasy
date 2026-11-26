namespace EasyReasy.Auth
{
    /// <summary>
    /// What a flow's state carries to find and check its <see cref="FlowBindingCookie"/>: the flow's id, which names the
    /// cookie, and the hash of the cookie's value. Neither lets anyone forge the cookie, so both may travel in a state
    /// the browser carries, as long as the state itself is encrypted or signed.
    /// </summary>
    public sealed class FlowBinding
    {
        /// <summary>
        /// Rebuilds a flow's binding from the two values its state carries.
        /// </summary>
        /// <param name="flowId">The flow's id, as <see cref="FlowBindingCookie.Issue"/> returned it.</param>
        /// <param name="valueHash">The cookie value's hash, as <see cref="FlowBindingCookie.Issue"/> returned it.</param>
        /// <exception cref="ArgumentException">Either value is null or blank.</exception>
        public FlowBinding(string flowId, string valueHash)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(flowId);
            ArgumentException.ThrowIfNullOrWhiteSpace(valueHash);

            FlowId = flowId;
            ValueHash = valueHash;
        }

        /// <summary>
        /// The flow's random id, which follows the prefix in the cookie's name.
        /// </summary>
        public string FlowId { get; }

        /// <summary>
        /// The hash of the cookie's value.
        /// </summary>
        public string ValueHash { get; }
    }
}
