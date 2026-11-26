namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class FlowBindingTests
    {
        [TestMethod]
        [DataRow("", "hash")]
        [DataRow("flow", " ")]
        public void Constructor_BlankPart_Throws(string flowId, string valueHash)
        {
            Assert.ThrowsException<ArgumentException>(() => new FlowBinding(flowId, valueHash));
        }
    }
}
