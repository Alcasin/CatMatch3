using UnityEditor;

namespace CatMatch3.Editor
{
    /// <summary>M3 uses the existing components and transient UI; no additional scene wiring is needed.</summary>
    public static class M3SpecialsSetup
    {
        [MenuItem("CatMatch3/Setup/Apply M3 Specials & Objectives")]
        public static void Apply() { M1BoardSetup.Apply(); }
    }
}
