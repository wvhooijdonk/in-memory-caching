using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using CM.Caching.Examples;
using Microsoft.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CM.Caching.Tests
{
    [TestClass]
    public class CacheCreatorTests
    {
        [TestMethod]
        public void TestAll()
        {
            var instance = ClassCache.Create<ISource, Source>(new Source(), "name of cache");
        }

        [TestMethod]
        public void GenerateSourceCode()
        {
            string className;
            var compileUnit = ClassCache.createCompileUnit<ISource>(out className);

            var provider = new CSharpCodeProvider(new Dictionary<String, String> { { "CompilerVersion", "v4.0" } });
            var firstTypeName = compileUnit.Namespaces[0].Types[0].Name;
            var sb = new StringBuilder();
            using (var sw = new StringWriter(sb))
            {
                var tw = new IndentedTextWriter(sw, "    ");
                provider.GenerateCodeFromCompileUnit(compileUnit, tw, new CodeGeneratorOptions());
                tw.Close();
            }
            Trace.TraceInformation(sb.ToString());
        }
    }
}
