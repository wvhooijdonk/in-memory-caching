using Microsoft.CSharp;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Caching;
using System.Threading;

namespace CM.Caching
{
    public static class ClassCache
    {
        private static Random randomizer = new Random();
        public static T Create<T, C>(C instance, string cacheName) where C : T
        {
            string classFullName;
            var compileUnit = createCompileUnit<T>(out classFullName);
            var assembly = compile(compileUnit);

            return (T)assembly.CreateInstance(
                classFullName, 
                false, 
                BindingFlags.CreateInstance, 
                null, 
                new object[] { instance, cacheName }, 
                Thread.CurrentThread.CurrentCulture, 
                null);
        }

        private static Assembly compile(CodeCompileUnit compileUnit)
        {
            var provider = new CSharpCodeProvider(new Dictionary<String, String> { { "CompilerVersion", "v4.0" } });
            var parameters = new CompilerParameters();
            parameters.ReferencedAssemblies.Add("System.dll");
            parameters.ReferencedAssemblies.Add("System.Runtime.Caching.dll");
            parameters.ReferencedAssemblies.Add("CM.Caching.dll");
            parameters.ReferencedAssemblies.Add(Assembly.GetAssembly(typeof(ClassCache)).ManifestModule.Name);
            //parameters.OutputAssembly = $"{Assembly.GetAssembly(typeof(CacheCreatorTests)).ManifestModule.Name}";
            parameters.GenerateInMemory = true;
            parameters.TreatWarningsAsErrors = true;
            parameters.CompilerOptions = "/optimize";
            var compileResults = provider.CompileAssemblyFromDom(parameters, compileUnit);

            foreach (CompilerError ce in compileResults.Errors)
                Trace.TraceError("Error: {0}", ce.ToString());

            if (compileResults.Errors.Count > 0)
                return null;

            Trace.TraceInformation("Compile succesfull");
            return compileResults.CompiledAssembly;
        }
        internal static CodeCompileUnit createCompileUnit<T>(out string classFullName)
        {
            var type = typeof(T);
            if (!type.IsInterface)
                throw new ArgumentException($"Only interfaces are suopported and '{type.FullName}' is not an interface");

            var compileUnit = new CodeCompileUnit();
            var nameSpace = new CodeNamespace(typeof(ClassCache).Namespace);
            nameSpace.Imports.Add(new CodeNamespaceImport("System"));
            nameSpace.Imports.Add(new CodeNamespaceImport("System.Runtime.Caching"));
            compileUnit.Namespaces.Add(nameSpace);
            var className = $"ClassCache{randomizer.Next(int.MaxValue)}";
            classFullName = $"{nameSpace.Name}.{className}";
            var cachedClass = implementClass(type, nameSpace, className);
            nameSpace.Types.Add(cachedClass);
            cachedClass.Members.Add(new CodeMemberField(typeof(IDictionary<string, ObjectCache>), "caches"));
            cachedClass.Members.Add(implementConstructor<T>());


            var methodInfos = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var methodInfo in methodInfos)
            {
                var memberMethod = implementMethod(methodInfo);
                cachedClass.Members.Add(memberMethod);
            }

            //TODO: implement caching
            return compileUnit;
        }

        private static CodeTypeDeclaration implementClass(Type type, CodeNamespace nameSpace, string className)
        {
            var cachedClass = new CodeTypeDeclaration(className);
            cachedClass.BaseTypes.Add(new CodeTypeReference(type.FullName));
            cachedClass.IsClass = true;
            cachedClass.TypeAttributes = TypeAttributes.Public | TypeAttributes.Sealed;
            return cachedClass;
        }

        private static void implementAssignmentOfConstructorParameter(CodeConstructor constructor, Type parameterType, string parameterName)
        {
            constructor.Parameters.Add(new CodeParameterDeclarationExpression(parameterType, parameterName));
            var instanceReference = new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), parameterName);
            constructor.Statements.Add(new CodeAssignStatement(instanceReference, new CodeArgumentReferenceExpression(parameterName)));
        }

        /// <summary>
        ///  public ClassCacheForISourceXXXXXXXXXXX(string cacheName)
        ///  {
        ///    cache = new MemoryCache(cacheName);
        ///  }
        /// </summary>
        private static CodeConstructor implementConstructor<T>()
        {
            var constructor = new CodeConstructor();
            constructor.Attributes = MemberAttributes.Public | MemberAttributes.Final;

            //implement assigning parameters to internal variables
            implementAssignmentOfConstructorParameter(constructor, typeof(T), "instance");
            implementAssignmentOfConstructorParameter(constructor, typeof(string), "cacheName");
            implementAssignmentOfConstructorParameter(constructor, typeof(Func<CacheItemPolicy>), "createCacheItemPolicy");
            
            var cachesReference = new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "caches");
            constructor.Statements.Add(
                new CodeAssignStatement(cachesReference, new CodeObjectCreateExpression(typeof(ConcurrentDictionary<string, MemoryCache>)))
            );
            return constructor;
            
        }
        private static CodeMemberMethod implementMethod(MethodInfo methodInfo)
        {
            var method = new CodeMemberMethod();
            method.Attributes = MemberAttributes.Public | MemberAttributes.Final;
            method.Name = methodInfo.Name;
            method.ReturnType = new CodeTypeReference(methodInfo.ReturnType);

            foreach (var parameterInfo in methodInfo.GetParameters())
            {
                if (parameterInfo.IsOptional && parameterInfo.HasDefaultValue)
                    Trace.TraceWarning($"The default value for parameter '{parameterInfo.Name}' in method '{methodInfo.Name}' was ignored");
                var parameterDeclarationExpression = new CodeParameterDeclarationExpression(parameterInfo.ParameterType, parameterInfo.Name);
                method.Parameters.Add(parameterDeclarationExpression);
            }

            var throwNotImplementedException = new CodeThrowExceptionStatement(new CodeObjectCreateExpression("System.NotImplementedException"));
            method.Statements.Add(throwNotImplementedException);

            return method;
        }
    }
}
