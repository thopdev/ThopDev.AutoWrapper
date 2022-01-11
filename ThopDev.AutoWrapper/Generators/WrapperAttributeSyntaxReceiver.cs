using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ThopDev.AutoWrapper.Generators
{
    public class WrapperAttributeSyntaxReceiver : ISyntaxReceiver
    {
        public List<ClassDeclarationSyntax>  ClassToAugments { get; private set; } = new List<ClassDeclarationSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
           
            // Business logic to decide what we're interested in goes here
            if (syntaxNode is ClassDeclarationSyntax cds && cds.AttributeLists.Any(attr => attr.Attributes.Any(x => x.Name.ToFullString() == "Wrap")))
            {
                ClassToAugments.Add(cds);
            }
        }
    }
}