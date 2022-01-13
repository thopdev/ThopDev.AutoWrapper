using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ThopDev.AutoWrapper.Generators
{
    [Generator]
    public class WrapperGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {

//            if (!Debugger.IsAttached)
      //      {
        //        Debugger.Launch();
      //      }
            context.RegisterForSyntaxNotifications(() => new WrapperAttributeSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var mainMethod = context.Compilation.GetEntryPoint(context.CancellationToken);

            // Build up the source code
            var source = $@" // Auto-generated code
using System;

namespace ThopDev.AutoWrapper
{{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class WrapAttribute : Attribute
    {{
        public WrapAttribute(Type typeInfo){{
        }}
    }}
}}

";
            // Add the source code to the compilation
            context.AddSource($"WrapAttribute.cs", source);
            var receiver = context.SyntaxReceiver as WrapperAttributeSyntaxReceiver;

            var sementicModel = context.Compilation.GetSemanticModel(receiver.ClassToAugments.First().SyntaxTree);

            foreach (var partialClass in receiver.ClassToAugments)
            {
               

                // ReSharper disable once PossibleNullReferenceException
                var classBuilder = new StringBuilder();
                using (var textWriter = new StringWriter(classBuilder))
                {
                    using (var indentWriter = new IndentedTextWriter(textWriter))
                    {
                        var argumentType = GetArgumentType(partialClass, sementicModel);

                        AddClassToIndentTextWriter(partialClass, argumentType, indentWriter);
                        context.AddSource($"{partialClass.Identifier.ValueText}.g.cs", classBuilder.ToString());
                        classBuilder.Clear();
                    }
                }

                var interfaceBuilder = new StringBuilder();
                using (var textWriter = new StringWriter(interfaceBuilder))
                {
                    using (var indentWriter = new IndentedTextWriter(textWriter))
                    {
                        var argumentType = GetArgumentType(partialClass, sementicModel);

                        AddInterfaceToIndentTextWriter(partialClass, argumentType, indentWriter);
                        context.AddSource($"I{partialClass.Identifier.ValueText}.g.cs", interfaceBuilder.ToString());
                        interfaceBuilder.Clear();
                    }
                }
            }
        }

        private void AddInterfaceToIndentTextWriter(ClassDeclarationSyntax partialClass, TypeInfo argumentTypeInfo,
            IndentedTextWriter indentWriter)
        {
            AddUsingsToIndentWriter(indentWriter, argumentTypeInfo);
            AddNamespaceToIndentWriter(indentWriter, partialClass);

            indentWriter.WriteLine($"public interface I{partialClass.Identifier.ValueText} {{");
            indentWriter.Indent++;
            indentWriter.WriteLine();

            foreach (var symbol in argumentTypeInfo.Type.GetMembers())
            {
                AddMethodToIndentWriter(indentWriter, argumentTypeInfo, (IMethodSymbol)symbol, false);
            }

            indentWriter.Indent--;
            indentWriter.WriteLine("}");
            indentWriter.Indent--;
            indentWriter.WriteLine("}");
        }

        private void AddClassToIndentTextWriter(ClassDeclarationSyntax partialClass, TypeInfo argumentTypeInfo,
            IndentedTextWriter indentWriter)
        {
            AddUsingsToIndentWriter(indentWriter, argumentTypeInfo);
            AddNamespaceToIndentWriter(indentWriter, partialClass);

            indentWriter.WriteLine($"public partial class {partialClass.Identifier.ValueText} {{");
            indentWriter.Indent++;
            indentWriter.WriteLine();

            AddFieldsToIndentWriter(indentWriter, argumentTypeInfo);

            indentWriter.WriteLine();
            indentWriter.WriteLine($@"public {partialClass.Identifier.ValueText}({argumentTypeInfo.Type.Name} wrapObject){{");
            indentWriter.Indent++;
            indentWriter.WriteLine($"_{argumentTypeInfo.Type.Name} = wrapObject;");
            indentWriter.Indent--;
            indentWriter.WriteLine("}");

            foreach (var symbol in argumentTypeInfo.Type.GetMembers())
            {
                AddMethodToIndentWriter(indentWriter, argumentTypeInfo, (IMethodSymbol)symbol, true);
            }

            indentWriter.Indent--;
            indentWriter.WriteLine("}");
            indentWriter.Indent--;
            indentWriter.WriteLine("}");
        }

        private static TypeInfo GetArgumentType(ClassDeclarationSyntax partialClass, SemanticModel sementicModel)
        {
            var argumentTypeExpressionSyntax = ((TypeOfExpressionSyntax)partialClass.AttributeLists.First().Attributes.First()
                .ArgumentList.Arguments.First().Expression).Type;
            var argumentType = sementicModel.GetTypeInfo(argumentTypeExpressionSyntax);
            return argumentType;
        }

        private static void AddFieldsToIndentWriter(IndentedTextWriter indentWriter, TypeInfo argumentType)
        {
            indentWriter.WriteLine($"private readonly {argumentType.Type.Name} _{argumentType.Type.Name};");
        }

        private static void AddNamespaceToIndentWriter(IndentedTextWriter indentWriter, SyntaxNode partialClass)
        {
            var namespaceDeclarationSyntax = partialClass.Parent as NamespaceDeclarationSyntax;
            var namespaceString = namespaceDeclarationSyntax.Name.ToString();

            indentWriter.WriteLine($"namespace {namespaceString} {{");
            indentWriter.Indent++;
        }

        private static void AddUsingsToIndentWriter(IndentedTextWriter indentWriter, TypeInfo argumentType)
        {
            var argumentNamespace = argumentType.Type.ContainingNamespace.ToString();

            indentWriter.WriteLine($"using {argumentNamespace};");
            indentWriter.WriteLine();
        }


        public void AddMethodToIndentWriter(IndentedTextWriter textWriter, TypeInfo typeInfo, IMethodSymbol method, bool renderMethodBody)
        {
            if (method.MethodKind == MethodKind.Constructor)
            {
                return;
            }

            textWriter.WriteLine();
            textWriter.Write($"{method.DeclaredAccessibility.ToString().ToLower()} {method.ReturnType.ToString().ToLower()} {method.Name}(");

            textWriter.Write(string.Join(", ", method.Parameters.Select(x => $"{x.Type.Name} {x.Name}")) + ")");

            if (renderMethodBody)
            {
                textWriter.WriteLine("{");
                textWriter.Indent++;
                textWriter.WriteLine($"_{typeInfo.Type.Name}.{method.Name}({string.Join(" ,", method.Parameters.Select(x => x.Name))});");
                textWriter.Indent--;

                textWriter.WriteLine("}");
                textWriter.WriteLine();
                return;
            }
            textWriter.WriteLine(";");
            textWriter.WriteLine();
        }
    }
}
