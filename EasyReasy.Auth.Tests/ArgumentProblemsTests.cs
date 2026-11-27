namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// <see cref="ArgumentProblems"/> reports every problem in one exception, and nothing when there is none.
    /// </summary>
    [TestClass]
    public class ArgumentProblemsTests
    {
        [TestMethod]
        public void ThrowIfAny_Problems_ListsEveryOneUnderTheSubject()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => ArgumentProblems.ThrowIfAny(["First is wrong.", "Second is wrong."], "WidgetOptions", "options"));

            StringAssert.StartsWith(exception.Message, "Invalid WidgetOptions:\nFirst is wrong.\nSecond is wrong.");
            Assert.AreEqual("options", exception.ParamName);
        }

        [TestMethod]
        public void ThrowIfAny_NoProblems_DoesNotThrow()
        {
            ArgumentProblems.ThrowIfAny([], "WidgetOptions");
        }

        [TestMethod]
        public void ThrowIfAny_BlankSubject_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => ArgumentProblems.ThrowIfAny([], " "));
        }
    }
}
