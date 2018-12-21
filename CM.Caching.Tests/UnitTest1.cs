using System;
using System.IO;
using System.Text;
using CM.Caching.Examples;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CM.Caching.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void CreateCompilationUnit()
        {
            var compilationUnit = ClassCacheRoslyn.CreateCompilationUnit<ISource>();
            var sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
            {
                compilationUnit.WriteTo(writer);
                Console.Write(writer.ToString());
            };
        }
    }
}
