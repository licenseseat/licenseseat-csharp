using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#if UNITY_2019_3_OR_NEWER
using UnityEditor.UnityLinker;
#endif

namespace LicenseSeat.Editor
{
    /// <summary>
    /// Ensures the LicenseSeat link.xml is included in IL2CPP builds.
    ///
    /// Unity does not automatically detect link.xml files in UPM packages.
    /// This processor injects our link.xml during the build process to prevent
    /// code stripping issues on iOS, Android, and WebGL platforms.
    ///
    /// See: https://docs.unity3d.com/ScriptReference/Build.IUnityLinkerProcessor.html
    /// </summary>
#if UNITY_2019_3_OR_NEWER
    public class LicenseSeatLinkerProcessor : IUnityLinkerProcessor
    {
        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            // Find the link.xml in our package
            var linkXmlPath = FindPackageLinkXml();

            if (string.IsNullOrEmpty(linkXmlPath))
            {
                UnityEngine.Debug.LogWarning(
                    "[LicenseSeat] Could not find link.xml in package. " +
                    "IL2CPP builds may have code stripping issues. " +
                    "Please ensure the LicenseSeat SDK package is properly installed.");
                return null;
            }

            return linkXmlPath;
        }

        private static string FindPackageLinkXml()
        {
            // Method 1: Find via package path (most reliable for UPM packages)
            var packagePath = GetPackagePath();
            if (!string.IsNullOrEmpty(packagePath))
            {
                var linkXmlPath = Path.Combine(packagePath, "link.xml");
                if (File.Exists(linkXmlPath))
                {
                    return linkXmlPath;
                }
            }

            // Method 2: Search in Packages folder (for local/embedded packages)
            var packagesLinkXml = Path.Combine("Packages", "com.licenseseat.sdk", "link.xml");
            if (File.Exists(packagesLinkXml))
            {
                return Path.GetFullPath(packagesLinkXml);
            }

            // Method 3: Search using AssetDatabase (works in all scenarios)
            var guids = AssetDatabase.FindAssets("link t:TextAsset", new[] { "Packages/com.licenseseat.sdk" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("link.xml"))
                {
                    return Path.GetFullPath(path);
                }
            }

            // Method 4: Fallback - search all packages
            var allGuids = AssetDatabase.FindAssets("link t:TextAsset", new[] { "Packages" });
            foreach (var guid in allGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("LicenseSeat") && path.EndsWith("link.xml"))
                {
                    return Path.GetFullPath(path);
                }
            }

            return null;
        }

        private static string GetPackagePath()
        {
#if UNITY_2019_4_OR_NEWER
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                "Packages/com.licenseseat.sdk/package.json");
            if (packageInfo != null)
            {
                return packageInfo.resolvedPath;
            }
#endif
            return null;
        }
    }
#endif
}
