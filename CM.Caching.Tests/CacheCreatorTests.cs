using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using CM.Caching;
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
        public void CompileAndInstantiate()
        {
            string className;
            var compileUnit = ClassCache.createCompileUnit<ISource>(out className);

            //save to file
            var provider = new CSharpCodeProvider(new Dictionary<String, String> { { "CompilerVersion", "v4.0" } });
            var firstTypeName = compileUnit.Namespaces[0].Types[0].Name;
            string sourceFile = $"Generated\\{firstTypeName}{(provider.FileExtension[0] == '.' ? string.Empty : ".")}{provider.FileExtension}";
            using (var sw = new StreamWriter(sourceFile, false))
            {
                var tw = new IndentedTextWriter(sw, "    ");
                provider.GenerateCodeFromCompileUnit(compileUnit, tw, new CodeGeneratorOptions());
                tw.Close();
            }
        }
    }
}
