﻿using Microsoft.CSharp;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Caching;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CM.Caching
{
    public static class ClassCache
    {
        private static Random randomizer = new Random();
        public static T Create<T, C>(C instance, string cacheName, Func<string, CacheItemPolicy> createCacheItemPolicy) where C : T
        {
            string classFullName;
            var compileUnit = createCompileUnit<T>(out classFullName);
            var assembly = compile<T>(compileUnit);

            return (T)assembly.CreateInstance(
                classFullName, 
                false, 
                BindingFlags.CreateInstance, 
                null, 
                new object[] { instance, cacheName, createCacheItemPolicy }, 
                Thread.CurrentThread.CurrentCulture, 
                null);
        }

        private static Assembly compile<T>(CodeCompileUnit compileUnit)
        {
            var provider = new CSharpCodeProvider(new Dictionary<String, String> { { "CompilerVersion", "v4.0" } });
            var parameters = new CompilerParameters();
            parameters.ReferencedAssemblies.Add("System.dll");
            parameters.ReferencedAssemblies.Add("System.Runtime.Caching.dll");
            //parameters.ReferencedAssemblies.Add("CM.Caching.dll");
            parameters.ReferencedAssemblies.Add(Assembly.GetAssembly(typeof(ClassCache)).ManifestModule.Name);
            parameters.ReferencedAssemblies.Add(Assembly.GetAssembly(typeof(T)).ManifestModule.Name);
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
                throw new ArgumentException($"Only interfaces are supported and '{type.FullName}' is not an interface");

            var compileUnit = new CodeCompileUnit();
            var nameSpace = new CodeNamespace(typeof(ClassCache).Namespace);
            nameSpace.Imports.Add(new CodeNamespaceImport("System"));
            //not necessary as method calls are referenced including namespace
            //nameSpace.Imports.Add(new CodeNamespaceImport("CM.Caching"));
            nameSpace.Imports.Add(new CodeNamespaceImport("System.Runtime.Caching"));
            //not necessary as T is referenced including namespace
            //nameSpace.Imports.Add(new CodeNamespaceImport(type.Namespace));

            compileUnit.Namespaces.Add(nameSpace);
            var className = $"ClassCache{randomizer.Next(int.MaxValue)}";
            classFullName = $"{nameSpace.Name}.{className}";
            var cachedClass = implementClass(type, nameSpace, className);
            nameSpace.Types.Add(cachedClass);
            cachedClass.Members.AddRange(implementPrivateVariables<T>(cachedClass));
            cachedClass.Members.Add(implementConstructor<T>());
            cachedClass.Members.AddRange(implementMethods(type, cachedClass));

            return compileUnit;
        }

        private static CodeMemberMethod[] implementMethods(Type type, CodeTypeDeclaration cachedClass)
        {
            var methods = new List<CodeMemberMethod>();
            var methodInfos = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var methodInfo in methodInfos)
                methods.Add(implementMethod(methodInfo));
            return methods.ToArray();
        }

        public static string MethodCallToKey(string methodName, params object[] parameters)
        {
            const string nullString = "(null)";
            const string separator = "|";
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(methodName);
            stringBuilder.Append(separator);
            foreach (var parameter in parameters)
            {
                stringBuilder.Append(parameter == null ? nullString : parameter.ToString());
                stringBuilder.Append(separator);
            }
            return stringBuilder.ToString();
        }

        private static CodeMemberField[] implementPrivateVariables<T>(CodeTypeDeclaration cachedClass)
        {
            return new CodeMemberField[] {
                new CodeMemberField(typeof(ConcurrentDictionary<string, ObjectCache>), "caches"),
                new CodeMemberField(typeof(T), "instance"),
                new CodeMemberField(typeof(string), "cacheName"),
                new CodeMemberField(typeof(Func<string, CacheItemPolicy>), "createCacheItemPolicy")
            };
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
            implementAssignmentOfConstructorParameter(constructor, typeof(Func<string, CacheItemPolicy>), "createCacheItemPolicy");
            
            var cachesReference = new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "caches");
            constructor.Statements.Add(new CodeAssignStatement(cachesReference, new CodeObjectCreateExpression(typeof(ConcurrentDictionary<string, ObjectCache>))));
            return constructor;
            
        }
        private static CodeMemberMethod implementMethod(MethodInfo methodInfo)
        {
            var method = new CodeMemberMethod();
            method.Attributes = MemberAttributes.Public | MemberAttributes.Final;
            method.Name = methodInfo.Name;
            method.ReturnType = new CodeTypeReference(methodInfo.ReturnType);

            
            var parameterReferences = new List<CodeExpression>();
            foreach (var parameterInfo in methodInfo.GetParameters())
            {
                if (parameterInfo.IsOptional && parameterInfo.HasDefaultValue)
                    Trace.TraceWarning($"The default value for parameter '{parameterInfo.Name}' in method '{methodInfo.Name}' was ignored");
                var parameterDeclarationExpression = new CodeParameterDeclarationExpression(parameterInfo.ParameterType, parameterInfo.Name);
                //store parameter references for later
                parameterReferences.Add(new CodeVariableReferenceExpression(parameterInfo.Name));
                method.Parameters.Add(parameterDeclarationExpression);
            }

            //method body
            var methodCallToKeyMethodParameters = parameterReferences;
            methodCallToKeyMethodParameters.Insert(0, new CodePrimitiveExpression(methodInfo.Name));
            var declareVariableCacheKey = new CodeVariableDeclarationStatement(typeof(string), "cacheKey");
            method.Statements.Add(declareVariableCacheKey);
            var methodCallToKeyMethod = new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(ClassCache)), "MethodCallToKey", methodCallToKeyMethodParameters.ToArray());
            var assignMethodCallToKeyResult = new CodeAssignStatement(new CodeVariableReferenceExpression("cacheKey"), methodCallToKeyMethod);
            method.Statements.Add(assignMethodCallToKeyResult);

            var debugWriteLineInvocation = new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Debug)), "WriteLine", new CodeVariableReferenceExpression("cacheKey"));
            method.Statements.Add(debugWriteLineInvocation);

            //var throwNotImplementedException = new CodeThrowExceptionStatement(new CodeObjectCreateExpression("System.NotImplementedException"));
            //method.Statements.Add(th

            CodeExpression returnValueExpression;
            if (methodInfo.ReturnType.BaseType == typeof(Task))
            {
                var innerTaskType = methodInfo.ReturnType.GenericTypeArguments[0];
                returnValueExpression = new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Task)), "FromResult", new CodeDefaultValueExpression(new CodeTypeReference(innerTaskType)));
            }
            else
                returnValueExpression = new CodeDefaultValueExpression(new CodeTypeReference(methodInfo.ReturnType));
            method.Statements.Add(new CodeMethodReturnStatement(returnValueExpression));
            return method;
        }
    }
}
