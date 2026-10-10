using UnityEditor;

namespace CatMatch3.Editor
{
    /// <summary>M2 needs no additional serialized objects; reuse the validated M1 scene setup.</summary>
    public static class M2ResolutionSetup
    {
        [MenuItem("CatMatch3/Setup/Apply M2 Resolution")]
        public static void Apply() { M1BoardSetup.Apply(); }
    }
}
