using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;
using CM.Caching.Examples;
using Microsoft.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CM.Caching.Tests
{
    [TestClass]
    public class CacheCreatorTests
    {
        [TestMethod]
        public async Task TestAll()
        {
            var cachedSource = ClassCache.Create<ISource, Source>(new Source(), "nameOfCache", () => new CacheItemPolicy() { AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(1) });

            var stringResult = cachedSource.GetData("GetDataParamValue");
            Console.WriteLine($"GetData: {stringResult}");

            var source = await cachedSource.GetDataAsync("GetDataAsyncParamValue");
            Console.WriteLine($"GetDataAsync: {source}");

            var intResult = cachedSource.GetSomeMoreData(null);
            Console.WriteLine($"GetSomeMoreData: {intResult}");
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
