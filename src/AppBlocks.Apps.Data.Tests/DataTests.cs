using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace AppBlocks.Tests
{
    [TestClass]
    public class DataTests
    {
        [TestMethod]
        public void TestMethod1()
        {
            var results = new AppBlocks.Apps.Data().App();

            Assert.IsTrue(results.Count > 0);
        }

        [TestMethod]
        public void CountTest()
        {
            var query = "select count(id) from items";
            var settings = new Dictionary<string, object>
                {
                    { "source", query },
                };
            var results = new AppBlocks.Apps.Data().App(settings);
            Assert.IsTrue(results.Count > 0 && !results[0].ContainsKey("Error"), !results[0].ContainsKey("Error") ? results?[0].ToString() : results?[0]?["Error"].ToString());
        }
    }
}
