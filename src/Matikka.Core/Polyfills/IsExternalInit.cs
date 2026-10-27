// netstandard2.1 ei sisällä tätä tyyppiä. C# 9:n record- ja init-ominaisuudet tarvitsevat sen.
// Unity käyttää samaa kiertotietä.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
