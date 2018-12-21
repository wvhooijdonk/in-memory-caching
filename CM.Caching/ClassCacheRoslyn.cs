using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Caching;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CM.Caching
{
    class ClassCacheRoslyn
    {
        private static Random randomizer = new Random();
        private static SyntaxGenerator generator = SyntaxGenerator.GetGenerator(new AdhocWorkspace(), LanguageNames.CSharp);
        private static CSharpCompilation compilation;

        public static SyntaxNode CreateCompilationUnit<T>()
        {
            var baseType = typeof(T);
            if (!baseType.IsInterface)
                throw new ArgumentException($"Only interfaces are supported. Type '{baseType.FullName}'");

            compilation = CSharpCompilation.Create("test", references: new[] {
                MetadataReference.CreateFromFile(typeof(ObjectCache).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(T).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(CacheItemPolicy).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Func<>).Assembly.Location)
            });
            
            var cacheField = generator.FieldDeclaration("cache", generator.TypeExpression(compilation.GetTypeByMetadataName(typeof(ObjectCache).FullName)), Accessibility.Private);
            var instanceField = generator.FieldDeclaration("instance", generator.TypeExpression(compilation.GetTypeByMetadataName(typeof(T).FullName)), Accessibility.Private);
            var cacheItemPolicyType = compilation.GetTypeByMetadataName(typeof(CacheItemPolicy).FullName);
            var funcType = compilation.GetTypeByMetadataName(typeof(Func<>).FullName);
            var createCacheItemPolicyField = generator.FieldDeclaration("createCacheItemPolicy", generator.TypeExpression(funcType.Construct(cacheItemPolicyType)), Accessibility.Private);

            // Generate parameters for the class' constructor
            var constructorParameters = new SyntaxNode[] {
                generator.ParameterDeclaration("cache",
                generator.TypeExpression(compilation.GetTypeByMetadataName(typeof(ObjectCache).FullName))),
                generator.ParameterDeclaration("instance",
                generator.TypeExpression(compilation.GetTypeByMetadataName(typeof(T).FullName))),
                generator.ParameterDeclaration("createCacheItemPolicy",
                generator.TypeExpression(funcType.Construct(cacheItemPolicyType)))
            };

            // Generate the constructor's method body
            var constructorBody = new SyntaxNode[] {
                generator.AssignmentStatement(generator.MemberAccessExpression(generator.ThisExpression(), generator.IdentifierName("cache")), generator.IdentifierName("cache")),
                generator.AssignmentStatement(generator.MemberAccessExpression(generator.ThisExpression(), generator.IdentifierName("instance")), generator.IdentifierName("instance")),
                generator.AssignmentStatement(generator.MemberAccessExpression(generator.ThisExpression(), generator.IdentifierName("createCacheItemPolicy")), generator.IdentifierName("createCacheItemPolicy"))
            };

            var className = $"ClassCache{randomizer.Next(int.MaxValue)}";
            // Generate the class' constructor
            var constructor = generator.ConstructorDeclaration(className,
              constructorParameters, Accessibility.Public,
              statements: constructorBody);

            var methods = implementMethods(baseType);

            // An array of SyntaxNode as the class members
            var members = new List<SyntaxNode>() {
                cacheField,
                instanceField,
                createCacheItemPolicyField,
                constructor };
            members.AddRange(methods);

            // Generate the class
            var classDefinition = generator.ClassDeclaration(
              className, typeParameters: null,
              accessibility: Accessibility.Public,
              modifiers: DeclarationModifiers.Sealed,
              baseType: generator.IdentifierName(baseType.FullName),
              members: members);

            // Declare a namespace
            var namespaceDeclaration = generator.NamespaceDeclaration(typeof(ClassCache).Namespace, classDefinition);

            // Get a CompilationUnit (code file) for the generated code
            return generator.CompilationUnit(
                generator.NamespaceImportDeclaration("System"), 
                namespaceDeclaration
            ).NormalizeWhitespace();
        }

        private static IEnumerable<SyntaxNode> implementMethods(Type type)
        {
            var methods = new List<SyntaxNode>();
            var methodInfos = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var methodInfo in methodInfos)
                methods.Add(implementMethod(methodInfo));
            return methods;
        }
        private static bool isTaskType(Type t)
        {
            return t.BaseType == typeof(Task);
        }

        private static SyntaxNode createTypeExpressionForType(Type t)
        {
            var namedTypeSymbol = compilation.GetTypeByMetadataName(t.FullName);
            if (Nullable.GetUnderlyingType(t) != null)
            { //when nullable
                var nullableType = compilation.GetTypeByMetadataName(typeof(Nullable<>).FullName);
                namedTypeSymbol = nullableType.Construct(compilation.GetTypeByMetadataName(Nullable.GetUnderlyingType(t).FullName));
            }
            if (isTaskType(t))
            {
                var taskType = compilation.GetTypeByMetadataName(typeof(Task<>).FullName);
                namedTypeSymbol = taskType.Construct(compilation.GetTypeByMetadataName(t.GetGenericArguments()[0].FullName));
            }
            return generator.TypeExpression(namedTypeSymbol);
        }

        private static SyntaxNode implementMethod(MethodInfo methodInfo)
        {
            var parameters = new List<SyntaxNode>();
            foreach (var parameterInfo in methodInfo.GetParameters())
            {
                Debug.WriteLine($"Adding parameter '{parameterInfo.Name}' of type '{parameterInfo.ParameterType.FullName}'");
                if (parameterInfo.IsOptional && parameterInfo.HasDefaultValue)
                    Trace.TraceWarning($"Default values are not supported. Method '{methodInfo.Name}', parameter '{parameterInfo.Name}'");
                var parameter = generator.ParameterDeclaration(name: parameterInfo.Name, type: createTypeExpressionForType(parameterInfo.ParameterType));
                parameters.Add(parameter);
            }

            //var methodCallToKeyMethodParameters = new List<CodeExpression>();
            //methodCallToKeyMethodParameters.AddRange(parameterReferences);
            //methodCallToKeyMethodParameters.Insert(0, new CodePrimitiveExpression(methodInfo.Name));

            ////string cacheKey;
            //var declareVariableCacheKey = new CodeVariableDeclarationStatement(typeof(string), "cacheKey");
            //method.Statements.Add(declareVariableCacheKey);

            ////cacheKey = CM.Caching.ClassCache.MethodCallToKey("<MethodName>", param1, param2, ...);
            //var methodCallToKeyMethod = new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(ClassCache)), "MethodCallToKey", methodCallToKeyMethodParameters.ToArray());
            //var assignMethodCallToKeyResult = new CodeAssignStatement(new CodeVariableReferenceExpression("cacheKey"), methodCallToKeyMethod);
            //method.Statements.Add(assignMethodCallToKeyResult);

            ////string valueFactory() { instance.GetData(myParameter); }           
            ////var gMethod = new CodeMemberMethod() { Name = "g", ReturnType = new CodeTypeReference(methodInfo.ReturnType) };
            ////gMethod.Statements.Add(new CodeMethodReturnStatement(new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeVariableReferenceExpression("instance"), methodInfo.Name), parameterReferences.ToArray())));
            ////method.Statements.Add(gMethod);

            //CodeExpression returnValueExpression;
            //if (methodInfo.ReturnType.BaseType == typeof(Task))
            //{
            //    var innerTaskType = methodInfo.ReturnType.GenericTypeArguments[0];
            //    returnValueExpression = new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(Task)), "FromResult", new CodeDefaultValueExpression(new CodeTypeReference(innerTaskType)));
            //}
            //else
            //    //return GetFromCache(cache, cacheKey, instanceMethodCall, createCacheItemPolicy);
            //    returnValueExpression = new CodeMethodInvokeExpression(
            //        new CodeMethodReferenceExpression(new CodeTypeReferenceExpression(typeof(ClassCache)), "GetFromCache", new CodeTypeReference(methodInfo.ReturnType)),
            //        new CodeVariableReferenceExpression("cache"),
            //        new CodeVariableReferenceExpression("cacheKey"),
            //        new CodeVariableReferenceExpression("valueFactory"),
            //        new CodeVariableReferenceExpression("createCacheItemPolicy"));

            //method.Statements.Add(new CodeMethodReturnStatement(returnValueExpression));

            var modifiers = DeclarationModifiers.Sealed;
            if (isTaskType(methodInfo.ReturnType))
            {
                Trace.TraceWarning($"Interface methods that declare the return type Task or Task<> will be decorated with the async keyword. Method '{methodInfo.Name}'");
                modifiers = modifiers | DeclarationModifiers.Async;
            }

            return generator.MethodDeclaration(
                name:methodInfo.Name,
                parameters: parameters, 
                typeParameters:null,
                returnType: createTypeExpressionForType(methodInfo.ReturnType),
                accessibility: Accessibility.Public, 
                modifiers: modifiers,
                statements: null
            );

        }

    }
}
