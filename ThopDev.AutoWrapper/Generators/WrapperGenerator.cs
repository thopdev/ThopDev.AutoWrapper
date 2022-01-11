using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
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

            if (!Debugger.IsAttached)
            {
                Debugger.Launch();
            }
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
            var typeName = mainMethod.ContainingType.Name;

            // Add the source code to the compilation
            context.AddSource($"WrapAttribute.cs", source);
            var receiver = context.SyntaxReceiver as WrapperAttributeSyntaxReceiver;

            var sementicModel = context.Compilation.GetSemanticModel(receiver.ClassToAugments.First().SyntaxTree);

            foreach (var partialClass in receiver.ClassToAugments)
            {
               

                // ReSharper disable once PossibleNullReferenceException
                var strBuilder = new StringBuilder();
                var textWriter = new StringWriter(strBuilder);
                var indentWriter = new IndentedTextWriter(textWriter);

                AddClassToIndentTextWriter(partialClass, sementicModel, indentWriter);
                context.AddSource($"{partialClass.Identifier.ValueText}.g.cs", strBuilder.ToString());
            }
        }

        private void AddClassToIndentTextWriter(ClassDeclarationSyntax partialClass, SemanticModel sementicModel,
            IndentedTextWriter indentWriter)
        {
            var namespaceDeclarationSyntax = partialClass.Parent as NamespaceDeclarationSyntax;
            var namespaceString = namespaceDeclarationSyntax.Name.ToString();

            var argumentTypeExpressionSyntax = ((TypeOfExpressionSyntax)partialClass.AttributeLists.First().Attributes.First().ArgumentList.Arguments.First().Expression).Type;
            var argumentType = sementicModel.GetTypeInfo(argumentTypeExpressionSyntax);
            
            AddUsingsToIndentWriter(indentWriter, argumentType);
            AddNamespaceToIndentWriter(indentWriter, namespaceString);

            indentWriter.WriteLine($"public partial class {partialClass.Identifier.ValueText} {{");
            indentWriter.Indent++;
            indentWriter.WriteLine();

            AddFieldsToIndentWriter(indentWriter, argumentType);

            indentWriter.WriteLine();
            indentWriter.WriteLine($@"public {partialClass.Identifier.ValueText}({argumentType.Type.Name} wrapObject){{");
            indentWriter.Indent++;
            indentWriter.WriteLine($"_{argumentType.Type.Name} = wrapObject;");
            indentWriter.Indent--;
            indentWriter.WriteLine("}");

            foreach (var symbol in argumentType.Type.GetMembers())
            {
                AddMethodToIndentWriter(indentWriter, argumentType, (IMethodSymbol)symbol);
            }

            indentWriter.Indent--;
            indentWriter.WriteLine("}");
            indentWriter.Indent--;
            indentWriter.WriteLine("}");
        }

        private static void AddFieldsToIndentWriter(IndentedTextWriter indentWriter, TypeInfo argumentType)
        {
            indentWriter.WriteLine($"private readonly {argumentType.Type.Name} _{argumentType.Type.Name};");
        }

        private static void AddNamespaceToIndentWriter(IndentedTextWriter indentWriter, string namespaceString)
        {
            indentWriter.WriteLine($"namespace {namespaceString} {{");
            indentWriter.Indent++;
        }

        private static void AddUsingsToIndentWriter(IndentedTextWriter indentWriter, TypeInfo argumentType)
        {
            var argumentNamespace = argumentType.Type.ContainingNamespace.ToString();

            indentWriter.WriteLine($"using {argumentNamespace};");
            indentWriter.WriteLine();
        }


        public void AddMethodToIndentWriter(IndentedTextWriter textWriter, TypeInfo typeInfo, IMethodSymbol method)
        {
            if (method.MethodKind == MethodKind.Constructor)
            {
                return;
            }

            textWriter.WriteLine();
            textWriter.Write($"{method.DeclaredAccessibility.ToString().ToLower()} {method.ReturnType.ToString().ToLower()} {method.Name}(");

            textWriter.Write(string.Join(", ", method.Parameters.Select(x => $"{x.Type.Name} {x.Name}")));

            textWriter.WriteLine("){");
            textWriter.Indent++;
            textWriter.WriteLine($"_{typeInfo.Type.Name}.{method.Name}({string.Join(" ,", method.Parameters.Select(x => x.Name))});");
            textWriter.Indent--;
            textWriter.WriteLine("}");
            textWriter.WriteLine();
        }
    }
}
