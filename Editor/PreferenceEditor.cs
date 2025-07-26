#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using System;

public class PreferenceEditor : EditorWindow
{
    private Vector2 scrollPosition;
    private Dictionary<string, PreferenceData> editorPrefs = new Dictionary<string, PreferenceData>();
    private Dictionary<string, PreferenceData> playerPrefs = new Dictionary<string, PreferenceData>();
    private Dictionary<string, string> editedValues = new Dictionary<string, string>();
    private Dictionary<string, bool> foldouts = new Dictionary<string, bool>();
    private Dictionary<string, bool> categoryFoldouts = new Dictionary<string, bool>();

    private bool showEditorPrefs = true;
    private bool showPlayerPrefs = true;
    private bool showUnusedKeys = false;
    private string searchFilter = "";

    [Serializable]
    public class PreferenceData
    {
        public object value;
        public string category;
        public bool isActive;
        public string detectedSource;
        public string registryPath;

        public PreferenceData(object val, string cat, bool active = true, string source = "Unknown", string regPath = "")
        {
            value = val;
            category = cat;
            isActive = active;
            detectedSource = source;
            registryPath = regPath;
        }
    }

    [MenuItem("TohruTheDragon/Preference Editor")]
    public static void ShowWindow()
    {
        GetWindow<PreferenceEditor>("Preference Editor");
    }

    private void OnEnable()
    {
        RefreshAllPrefs();
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginVertical();

        // Header with controls
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh All", GUILayout.Width(100)))
        {
            RefreshAllPrefs();
        }

        GUILayout.Space(10);
        GUILayout.Label("Search:", GUILayout.Width(50));
        searchFilter = EditorGUILayout.TextField(searchFilter);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        showEditorPrefs = EditorGUILayout.Toggle("Show EditorPrefs", showEditorPrefs);
        showPlayerPrefs = EditorGUILayout.Toggle("Show PlayerPrefs", showPlayerPrefs);
        showUnusedKeys = EditorGUILayout.Toggle("Show Unused Keys", showUnusedKeys);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // EditorPrefs Section with categories
        if (showEditorPrefs)
        {
            DrawCategorizedPrefsSection("EditorPrefs", editorPrefs, true);
        }

        // PlayerPrefs Section with categories
        if (showPlayerPrefs)
        {
            DrawCategorizedPrefsSection("PlayerPrefs", playerPrefs, false);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawCategorizedPrefsSection(string sectionName, Dictionary<string, PreferenceData> prefs, bool isEditorPrefs)
    {
        EditorGUILayout.LabelField(sectionName, EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        // Group by category and sort
        var groupedPrefs = prefs
            .Where(kvp => showUnusedKeys || kvp.Value.isActive)
            .Where(kvp => string.IsNullOrEmpty(searchFilter) ||
                         kvp.Key.ToLower().Contains(searchFilter.ToLower()) ||
                         kvp.Value.value.ToString().ToLower().Contains(searchFilter.ToLower()) ||
                         kvp.Value.category.ToLower().Contains(searchFilter.ToLower()))
            .GroupBy(kvp => kvp.Value.category)
            .OrderBy(g => GetCategoryPriority(g.Key))
            .ThenBy(g => g.Key);

        int totalCount = prefs.Count;
        int activeCount = prefs.Count(kvp => kvp.Value.isActive);
        int unusedCount = totalCount - activeCount;

        EditorGUILayout.LabelField($"Total: {totalCount} | Active: {activeCount} | Unused: {unusedCount}", EditorStyles.miniLabel);

        foreach (var categoryGroup in groupedPrefs)
        {
            DrawCategoryGroup(categoryGroup.Key, categoryGroup.OrderBy(kvp => kvp.Key).ToList(), isEditorPrefs);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }

    private int GetCategoryPriority(string category)
    {
        switch (category)
        {
            case "Unity Core": return 0;
            case "Unity Editor": return 1;
            case "Unity Graphics": return 2;
            case "Unity Audio": return 3;
            case "Unity Physics": return 4;
            case "Unity Analytics": return 5;
            case "Unity Cloud": return 6;
            case "VRChat Tools": return 7;
            case "Third-Party": return 9;
            case "Custom/Unknown": return 20;
            case "Unused": return 30;
            default: return 15;
        }
    }

    private void DrawCategoryGroup(string category, List<KeyValuePair<string, PreferenceData>> prefs, bool isEditorPrefs)
    {
        if (prefs.Count == 0) return;

        string categoryKey = $"{category}_{(isEditorPrefs ? "editor" : "player")}";
        if (!categoryFoldouts.ContainsKey(categoryKey))
            categoryFoldouts[categoryKey] = false;

        EditorGUILayout.BeginVertical("box");

        // Category header with count and color coding
        EditorGUILayout.BeginHorizontal();
        GUI.color = GetCategoryColor(category);
        categoryFoldouts[categoryKey] = EditorGUILayout.Foldout(categoryFoldouts[categoryKey],
            $"{category} ({prefs.Count})", true, EditorStyles.foldoutHeader);
        GUI.color = Color.white;

        // Show category info
        if (prefs.Count > 0)
        {
            string sourceInfo = prefs.First().Value.detectedSource;
            if (prefs.Select(p => p.Value.detectedSource).Distinct().Count() > 1)
                sourceInfo = "Multiple Sources";

            EditorGUILayout.LabelField($"[{sourceInfo}]", EditorStyles.miniLabel, GUILayout.Width(150));
        }

        EditorGUILayout.EndHorizontal();

        if (categoryFoldouts[categoryKey])
        {
            EditorGUI.indentLevel++;
            foreach (var kvp in prefs)
            {
                DrawPrefItem(kvp.Key, kvp.Value, isEditorPrefs);
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private Color GetCategoryColor(string category)
    {
        switch (category)
        {
            case "Unity Core": return Color.green;
            case "Unity Editor": return Color.cyan;
            case "Unity Graphics": return Color.magenta;
            case "Unity Audio": return Color.yellow;
            case "Unity Physics": return new Color(1f, 0.5f, 0f); // Orange
            case "Unity Analytics": return new Color(0.5f, 0.5f, 1f); // Light blue
            case "Unity Cloud": return new Color(0.5f, 1f, 0.5f); // Light green
            case "Third-Party": return new Color(1f, 0.7f, 0.7f); // Light red
            case "Custom/Unknown": return Color.gray;
            case "Unused": return Color.red;
            default: return Color.white;
        }
    }

    private void DrawPrefItem(string key, PreferenceData prefData, bool isEditorPrefs)
    {
        int MAX_KEY_LENGTH = 60;
        string foldoutKey = key + (isEditorPrefs ? "_editor" : "_player");

        if (!foldouts.ContainsKey(foldoutKey))
            foldouts[foldoutKey] = false;

        // Dim unused preferences
        if (!prefData.isActive)
            GUI.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);

        EditorGUILayout.BeginVertical("box");

        // Header with foldout, key name, status, and delete button
        EditorGUILayout.BeginHorizontal();

        // Foldout with truncated key name if too long
        string displayKey = key;
        if (key.Length > MAX_KEY_LENGTH)
        {
            displayKey = key.Substring(0, MAX_KEY_LENGTH-3) + "...";
        }

        // Add unused prefix to the display name
        if (!prefData.isActive)
        {
            GUI.color = Color.red;
            displayKey = "[UNUSED] " + displayKey;
            GUI.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
        }

        foldouts[foldoutKey] = EditorGUILayout.Foldout(foldouts[foldoutKey], displayKey, true);

        GUILayout.FlexibleSpace();

        // Type indicator
        string typeStr = GetValueTypeString(prefData.value);
        GUI.color = GetTypeColor(typeStr);
        EditorGUILayout.LabelField($"[{typeStr}]", GUILayout.Width(60));
        GUI.color = Color.white;

        // Delete button
        GUI.color = Color.red;
        if (GUILayout.Button("Delete", GUILayout.Width(60)))
        {
            if (EditorUtility.DisplayDialog("Delete Pref",
                $"Are you sure you want to delete '{key}'?\nCategory: {prefData.category}\nSource: {prefData.detectedSource}", "Yes", "No"))
            {
                DeletePref(key, isEditorPrefs);
            }
        }
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        // Expanded content...
        if (foldouts[foldoutKey])
        {
            EditorGUI.indentLevel++;

            // Preference info
            EditorGUILayout.LabelField("Key:", key, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Category:", prefData.category);
            EditorGUILayout.LabelField("Detected Source:", prefData.detectedSource);
            EditorGUILayout.LabelField("Status:", prefData.isActive ? "Active" : "Unused");
            EditorGUILayout.LabelField("Current Value:", EditorStyles.boldLabel);

            string editKey = key + (isEditorPrefs ? "_editor" : "_player");
            if (!editedValues.ContainsKey(editKey))
                editedValues[editKey] = prefData.value.ToString();

            // Multi-line text area for editing
            EditorGUILayout.LabelField("Edit Value:");
            editedValues[editKey] = EditorGUILayout.TextArea(editedValues[editKey], GUILayout.MinHeight(60));

            // Save and Reset buttons
            EditorGUILayout.BeginHorizontal();

            // Save button
            GUI.color = Color.green;
            if (GUILayout.Button("Save Changes"))
            {
                SavePrefValue(key, editedValues[editKey], prefData.value, isEditorPrefs);
            }
            GUI.color = Color.white;

            // Reset button
            if (GUILayout.Button("Reset to Original"))
            {
                // Reset the text field
                editedValues[editKey] = prefData.value.ToString();

                // Actually save the original value back to the preference system
                SavePrefValue(key, prefData.value.ToString(), prefData.value, isEditorPrefs);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();

        if (!prefData.isActive)
            GUI.color = Color.white; // Reset color
    }

    private void SavePrefValue(string key, string newValue, object originalValue, bool isEditorPrefs)
    {
        try
        {
            if (isEditorPrefs)
            {
                // Find which registry path this key came from
                string originalRegistryPath = FindOriginalRegistryPath(key);

                if (!string.IsNullOrEmpty(originalRegistryPath))
                {
                    // Write directly to the original registry location
                    WriteToRegistry(originalRegistryPath, key, newValue, originalValue);
                }
                else
                {
                    // Fallback to Unity's built-in method
                    WriteViaUnityEditorPrefs(key, newValue, originalValue);
                }
            }
            else
            {
                // For PlayerPrefs, try to find original registry path first
                string originalRegistryPath = FindOriginalPlayerPrefsRegistryPath(key);

                if (!string.IsNullOrEmpty(originalRegistryPath))
                {
                    // Write directly to the original registry location
                    WriteToRegistry(originalRegistryPath, key, newValue, originalValue);
                }
                else
                {
                    // Fallback to Unity's built-in method
                    WriteViaUnityPlayerPrefs(key, newValue, originalValue);
                }
            }

            RefreshAllPrefs();
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to save value: {e.Message}", "OK");
        }
    }

    private string FindOriginalRegistryPath(string key)
    {
        string[] editorPrefPaths = {
        $"Software\\Unity\\UnityEditor\\{Application.companyName}\\{Application.productName}",
        "Software\\Unity\\UnityEditor5.x",
        "Software\\Unity\\UnityEditor",
        "Software\\Unity Technologies\\Unity Editor 5.x",
        "Software\\Unity Technologies\\Unity Editor"
    };

        foreach (string path in editorPrefPaths)
        {
            using (RegistryKey regKey = Registry.CurrentUser.OpenSubKey(path))
            {
                if (regKey != null && regKey.GetValue(key) != null)
                {
                    return path;
                }
            }
        }
        return null;
    }

    private string FindOriginalPlayerPrefsRegistryPath(string key)
    {
        string companyName = Application.companyName;
        string productName = Application.productName;

        string[] playerPrefPaths = {
        $"Software\\{companyName}\\{productName}",
        $"Software\\DefaultCompany\\{productName}",
        $"Software\\{productName}",
        $"Software\\Unity\\UnityEditor\\{companyName}\\{productName}",
        $"Software\\Unity Technologies\\{productName}",
    };

        foreach (string path in playerPrefPaths)
        {
            using (RegistryKey regKey = Registry.CurrentUser.OpenSubKey(path))
            {
                if (regKey != null && regKey.GetValue(key) != null)
                {
                    return path;
                }
            }
        }
        return null;
    }

    private void WriteToRegistry(string registryPath, string key, string newValue, object originalValue)
    {
        using (RegistryKey regKey = Registry.CurrentUser.OpenSubKey(registryPath, true))
        {
            if (regKey != null)
            {
                if (originalValue is int)
                    regKey.SetValue(key, int.Parse(newValue), RegistryValueKind.DWord);
                else if (originalValue is float)
                    regKey.SetValue(key, float.Parse(newValue).ToString(), RegistryValueKind.String);
                else if (originalValue is bool)
                    regKey.SetValue(key, bool.Parse(newValue) ? 1 : 0, RegistryValueKind.DWord);
                else
                    regKey.SetValue(key, newValue, RegistryValueKind.String);
            }
        }
    }

    private void WriteViaUnityEditorPrefs(string key, string newValue, object originalValue)
    {
        if (originalValue is int)
            EditorPrefs.SetInt(key, int.Parse(newValue));
        else if (originalValue is float)
            EditorPrefs.SetFloat(key, float.Parse(newValue));
        else if (originalValue is bool)
            EditorPrefs.SetBool(key, bool.Parse(newValue));
        else
            EditorPrefs.SetString(key, newValue);
    }

    private void WriteViaUnityPlayerPrefs(string key, string newValue, object originalValue)
    {
        if (originalValue is int)
        {
            PlayerPrefs.SetInt(key, int.Parse(newValue));
        }
        else if (originalValue is float)
        {
            PlayerPrefs.SetFloat(key, float.Parse(newValue));
        }
        else if (originalValue is bool)
        {
            // PlayerPrefs doesn't have SetBool, so we use int (0 = false, 1 = true)
            PlayerPrefs.SetInt(key, bool.Parse(newValue) ? 1 : 0);
        }
        else
        {
            PlayerPrefs.SetString(key, newValue);
        }

        PlayerPrefs.Save();
    }

    private void DeletePref(string key, bool isEditorPrefs)
    {
        if (isEditorPrefs)
        {
            EditorPrefs.DeleteKey(key);
        }
        else
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        RefreshAllPrefs();
    }

    private void RefreshAllPrefs()
    {
        editorPrefs.Clear();
        playerPrefs.Clear();
        editedValues.Clear();

        LoadEditorPrefs();
        LoadPlayerPrefs();
    }

    #region Dynamic Preference Discovery

    private void LoadEditorPrefs()
    {
        try
        {
            var discoveredPrefs = DiscoverAllEditorPrefs();
            foreach (var kvp in discoveredPrefs)
            {
                editorPrefs[kvp.Key] = kvp.Value;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading EditorPrefs: {e.Message}");
        }
    }

    private void LoadPlayerPrefs()
    {
        try
        {
            var discoveredPrefs = DiscoverAllPlayerPrefs();
            foreach (var kvp in discoveredPrefs)
            {
                playerPrefs[kvp.Key] = kvp.Value;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading PlayerPrefs: {e.Message}");
        }
    }

    private Dictionary<string, PreferenceData> DiscoverAllEditorPrefs()
    {
        var discovered = new Dictionary<string, PreferenceData>();

        try
        {
#if UNITY_EDITOR_WIN
            // Search registry for all Unity-related keys
            string[] editorPrefPaths = {
                $"Software\\Unity\\UnityEditor\\{Application.companyName}\\{Application.productName}",
                "Software\\Unity\\UnityEditor5.x",
                "Software\\Unity\\UnityEditor",
                "Software\\Unity Technologies\\Unity Editor 5.x",
                "Software\\Unity Technologies\\Unity Editor"
            };

            foreach (string path in editorPrefPaths)
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(path))
                {
                    if (key != null)
                    {
                        foreach (string valueName in key.GetValueNames())
                        {
                            object registryValue = key.GetValue(valueName);

                            if (!discovered.ContainsKey(valueName))
                            {
                                // Check if the key is actually used by Unity
                                bool isActive = EditorPrefs.HasKey(valueName);
                                object convertedValue = ConvertRegistryValueToUnityType(valueName, registryValue, true);
                                string category = CategorizeEditorPref(valueName);
                                string source = DetectPreferenceSource(valueName, path);

                                discovered[valueName] = new PreferenceData(convertedValue, category, isActive, source, path);
                            }
                        }
                    }
                }
            }

            // Search for third-party addons in registry
            SearchRegistryRecursively("Software\\Unity", discovered, true);
            SearchRegistryRecursively("Software\\Unity Technologies", discovered, true);

#elif UNITY_EDITOR_OSX
            discovered = DiscoverMacEditorPrefs();
#else
            discovered = DiscoverLinuxEditorPrefs();
#endif
        }
        catch (Exception e)
        {
            Debug.LogError($"Error discovering EditorPrefs: {e.Message}");
        }

        return discovered;
    }

    private Dictionary<string, PreferenceData> DiscoverAllPlayerPrefs()
    {
        var discovered = new Dictionary<string, PreferenceData>();

        try
        {
#if UNITY_EDITOR_WIN
            string companyName = Application.companyName;
            string productName = Application.productName;

            string[] playerPrefPaths = {
                $"Software\\{companyName}\\{productName}",
                $"Software\\DefaultCompany\\{productName}",
                $"Software\\{productName}",
                $"Software\\Unity\\UnityEditor\\{companyName}\\{productName}",
                $"Software\\Unity Technologies\\{productName}",
            };

            foreach (string path in playerPrefPaths)
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(path))
                {
                    if (key != null)
                    {
                        foreach (string valueName in key.GetValueNames())
                        {
                            object registryValue = key.GetValue(valueName);

                            if (!discovered.ContainsKey(valueName))
                            {
                                bool isActive = PlayerPrefs.HasKey(valueName);
                                object convertedValue = ConvertRegistryValueToUnityType(valueName, registryValue, false);
                                string category = CategorizePlayerPref(valueName);
                                string source = DetectPreferenceSource(valueName, path);

                                discovered[valueName] = new PreferenceData(convertedValue, category, isActive, source, path);
                            }
                        }
                    }
                }
            }

            SearchRegistryRecursively("Software", discovered, false, companyName, productName);

#elif UNITY_EDITOR_OSX
            discovered = DiscoverMacPlayerPrefs();
#else
            discovered = DiscoverLinuxPlayerPrefs();
#endif
        }
        catch (Exception e)
        {
            Debug.LogError($"Error discovering PlayerPrefs: {e.Message}");
        }

        return discovered;
    }

    private string CategorizeEditorPref(string key)
    {
        string prefix = ExtractKeyPrefix(key);
        string lowerPrefix = prefix.ToLower();
        string lowerKey = key.ToLower();

        // FIRST: Check if it's from a specific addon/source - this takes priority
        string addonSource = DetectAddonByPrefix(prefix, key);
        if (addonSource != "Unknown")
        {
            // Return a category based on the addon, not the functionality
            if (addonSource.Contains("VRChat"))
                return "VRChat Tools";
            return "Third-Party";
        }

        // SECOND: Only categorize by Unity built-ins if it's actually Unity
        if (IsUnityBuiltinPrefix(prefix))
        {
            if (lowerPrefix.Contains("graphics") || lowerPrefix.Contains("render") ||
                lowerPrefix.Contains("shader") || lowerPrefix.Contains("lighting"))
                return "Unity Graphics";
            if (lowerPrefix.Contains("audio") || lowerPrefix.Contains("sound"))
                return "Unity Audio";
            if (lowerPrefix.Contains("physics"))
                return "Unity Physics";
            if (lowerPrefix.Contains("editor"))
                return "Unity Editor";
            return "Unity Core";
        }

        // THIRD: Generic categorization only for truly unknown items
        return "Custom/Unknown";
    }

    private string CategorizePlayerPref(string key)
    {
        key = key.ToLower();

        // Unity built-in PlayerPrefs
        if (key.Contains("screenmanager") || key.Contains("unitygraphicsquality") ||
            key.Contains("unityselect") || key.StartsWith("unity."))
            return "Unity Core";

        // Graphics settings
        if (key.Contains("resolution") || key.Contains("fullscreen") ||
            key.Contains("graphics") || key.Contains("quality") || key.Contains("vsync"))
            return "Unity Graphics";

        // Audio settings  
        if (key.Contains("volume") || key.Contains("audio") || key.Contains("sound"))
            return "Unity Audio";

        // Third-party detection
        if (DetectThirdPartyAddon(key) != "Unknown")
            return "Third-Party";

        return "Custom/Unknown";
    }

    private string DetectThirdPartyAddon(string key)
    {
        key = key.ToLower();

        // Common Unity addons/packages
        if (key.Contains("odin") || key.Contains("sirenix"))
            return "Odin Inspector";
        if (key.Contains("dotween") || key.Contains("demigiant"))
            return "DOTween";
        if (key.Contains("playmaker") || key.Contains("hutong"))
            return "PlayMaker";
        if (key.Contains("amplify") || key.Contains("shader"))
            return "Amplify Shader Editor";
        if (key.Contains("probuilder") || key.Contains("progrids"))
            return "ProBuilder/ProGrids";
        if (key.Contains("textmeshpro") || key.Contains("tmp"))
            return "TextMeshPro";
        if (key.Contains("cinemachine"))
            return "Cinemachine";
        if (key.Contains("timeline"))
            return "Timeline";
        if (key.Contains("addressable"))
            return "Addressables";
        if (key.Contains("inputsystem") || key.Contains("newinput"))
            return "Input System";
        if (key.Contains("urp") || key.Contains("universal"))
            return "Universal RP";
        if (key.Contains("hdrp") || key.Contains("highdefinition"))
            return "HD RP";
        if (key.Contains("visualscripting") || key.Contains("bolt"))
            return "Visual Scripting";

        return "Unknown";
    }

    private string DetectPreferenceSource(string key, string registryPath)
    {
        // Extract prefix pattern (everything before first underscore or dot)
        string prefix = ExtractKeyPrefix(key);

        // Check for known addon prefixes
        string addonSource = DetectAddonByPrefix(prefix, key);
        if (addonSource != "Unknown")
            return addonSource;

        // Check for Unity built-in prefixes
        if (IsUnityBuiltinPrefix(prefix))
            return "Unity Core";

        // Custom project detection
        if (registryPath.Contains(Application.companyName) && !registryPath.Contains("Unity"))
            return $"Custom ({Application.companyName})";

        // Default fallback
        return $"Custom/Unknown ({prefix})";
    }

    private string ExtractKeyPrefix(string key)
    {
        // Common separators used in Unity preference keys
        char[] separators = { '_', '.', '-', '\\', '/' };

        foreach (char separator in separators)
        {
            int index = key.IndexOf(separator);
            if (index > 0)
            {
                return key.Substring(0, index);
            }
        }

        // If no separator found, check for camelCase boundaries
        for (int i = 1; i < key.Length; i++)
        {
            if (char.IsUpper(key[i]) && char.IsLower(key[i - 1]))
            {
                return key.Substring(0, i);
            }
        }

        // Return the whole key if no pattern detected
        return key;
    }

    private string DetectAddonByPrefix(string prefix, string fullKey)
    {
        prefix = prefix.ToLower();
        string lowerKey = fullKey.ToLower();

        // VRChat SDK and related tools
        if (prefix.Contains("vrc") || prefix.Contains("vrchat"))
            return "VRChat SDK";
        if (prefix.Contains("avatar") && lowerKey.Contains("vrc"))
            return "VRChat Avatar Tools";
        if (prefix.Contains("udon") || prefix.Contains("vrchat"))
            return "VRChat Udon";

        // Popular Unity addons by prefix
        if (prefix.Contains("odin") || prefix.Contains("sirenix"))
            return "Odin Inspector";
        if (prefix.Contains("dotween") || prefix.Contains("demigiant"))
            return "DOTween";
        if (prefix.Contains("playmaker") || prefix.Contains("hutong"))
            return "PlayMaker";
        if (prefix.Contains("amplify"))
            return "Amplify Tools";
        if (prefix.Contains("probuilder") || prefix.Contains("progrids"))
            return "ProBuilder";
        if (prefix.Contains("cinemachine"))
            return "Cinemachine";
        if (prefix.Contains("addressable"))
            return "Addressables";

        // Generic patterns
        if (prefix.Contains("editor") && !lowerKey.Contains("unity"))
            return $"Editor Tool ({prefix})";
        if (prefix.Contains("tool") || prefix.Contains("utility"))
            return $"Utility ({prefix})";
        if (prefix.Length <= 3 && prefix.All(char.IsUpper))
            return $"Addon ({prefix})"; // Like "SDK", "VRC", etc.

        return "Unknown";
    }

    private bool IsUnityBuiltinPrefix(string prefix)
    {
        string[] unityPrefixes = {
        "Unity", "UnityEditor", "UnityEngine", "ProjectBrowser",
        "SceneView", "Hierarchy", "Inspector", "Console", "Animation",
        "Lightmapping", "PlayerSettings", "BuildSettings", "Graphics",
        "Audio", "Physics", "Rendering", "Shader", "Material", "Texture"
    };

        return unityPrefixes.Any(up => prefix.StartsWith(up, StringComparison.OrdinalIgnoreCase));
    }

#if UNITY_EDITOR_WIN
    private void SearchRegistryRecursively(string basePath, Dictionary<string, PreferenceData> discovered, bool isEditorPrefs, string companyName = null, string productName = null)
    {
        try
        {
            using (RegistryKey baseKey = Registry.CurrentUser.OpenSubKey(basePath))
            {
                if (baseKey != null)
                {
                    foreach (string subKeyName in baseKey.GetSubKeyNames())
                    {
                        bool isRelevant = subKeyName.Contains("Unity");

                        if (!isEditorPrefs && companyName != null && productName != null)
                        {
                            isRelevant = isRelevant || subKeyName.Contains(companyName) ||
                                        subKeyName.Contains(productName) || subKeyName.Contains("DefaultCompany");
                        }

                        if (isRelevant)
                        {
                            string subKeyPath = $"{basePath}\\{subKeyName}";
                            using (RegistryKey subKey = Registry.CurrentUser.OpenSubKey(subKeyPath))
                            {
                                if (subKey != null)
                                {
                                    foreach (string valueName in subKey.GetValueNames())
                                    {
                                        object registryValue = subKey.GetValue(valueName);
                                        if (!discovered.ContainsKey(valueName))
                                        {
                                            bool isActive = isEditorPrefs ? EditorPrefs.HasKey(valueName) : PlayerPrefs.HasKey(valueName);
                                            object convertedValue = ConvertRegistryValueToUnityType(valueName, registryValue, isEditorPrefs);
                                            string category = isEditorPrefs ? CategorizeEditorPref(valueName) : CategorizePlayerPref(valueName);
                                            string source = DetectPreferenceSource(valueName, subKeyPath);

                                            discovered[valueName] = new PreferenceData(convertedValue, category, isActive, source, subKeyPath);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Error searching registry recursively: {e.Message}");
        }
    }
#endif

#if UNITY_EDITOR_OSX
    private Dictionary<string, PreferenceData> DiscoverMacEditorPrefs()
    {
        var discovered = new Dictionary<string, PreferenceData>();
        
        try
        {
            var process = new Process();
            process.StartInfo.FileName = "find";
            process.StartInfo.Arguments = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/Library/Preferences -name '*unity*' -o -name '*Unity*'";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            
            foreach (string plistPath in output.Split('\n'))
            {
                if (!string.IsNullOrEmpty(plistPath) && File.Exists(plistPath))
                {
                    ReadMacPlist(plistPath, discovered, true);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error discovering Mac EditorPrefs: {e.Message}");
        }
        
        return discovered;
    }

    private Dictionary<string, PreferenceData> DiscoverMacPlayerPrefs()
    {
        var discovered = new Dictionary<string, PreferenceData>();
        
        try
        {
            string companyName = Application.companyName.Replace(" ", "");
            string productName = Application.productName.Replace(" ", "");
            
            string plistPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/Library/Preferences/unity.{companyName}.{productName}.plist";
            
            if (File.Exists(plistPath))
            {
                ReadMacPlist(plistPath, discovered, false);
            }
            else
            {
                var process = new Process();
                process.StartInfo.FileName = "find";
                process.StartInfo.Arguments = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/Library/Preferences -name '*{productName}*' -o -name '*{companyName}*'";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                
                foreach (string foundPlistPath in output.Split('\n'))
                {
                    if (!string.IsNullOrEmpty(foundPlistPath) && File.Exists(foundPlistPath))
                    {
                        ReadMacPlist(foundPlistPath, discovered, false);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error discovering Mac PlayerPrefs: {e.Message}");
        }
        
        return discovered;
    }

    private void ReadMacPlist(string plistPath, Dictionary<string, PreferenceData> discovered, bool isEditorPrefs)
    {
        try
        {
            var process = new Process();
            process.StartInfo.FileName = "plutil";
            process.StartInfo.Arguments = $"-convert json -o - \"{plistPath}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            
            string jsonOutput = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            
            if (process.ExitCode == 0 && !string.IsNullOrEmpty(jsonOutput))
            {
                ParseMacPlistJson(jsonOutput, discovered, isEditorPrefs, plistPath);
            }
        }
        catch (Exception e)
        {
            LogWarning($"Could not read plist {plistPath}: {e.Message}");
        }
    }

    private void ParseMacPlistJson(string jsonOutput, Dictionary<string, PreferenceData> discovered, bool isEditorPrefs, string sourcePath)
    {
        try
        {
            var matches = Regex.Matches(jsonOutput, @"""([^""]+)""\s*:\s*([^,}]+)");
            
            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 3)
                {
                    string key = match.Groups[1].Value;
                    string valueStr = match.Groups[2].Value.Trim().Trim('"');
                    
                    bool isActive = isEditorPrefs ? EditorPrefs.HasKey(key) : PlayerPrefs.HasKey(key);
                    if (!discovered.ContainsKey(key))
                    {
                        object convertedValue = ConvertStringToUnityType(key, valueStr, isEditorPrefs);
                        string category = isEditorPrefs ? CategorizeEditorPref(key) : CategorizePlayerPref(key);
                        string source = Path.GetFileName(sourcePath);
                        
                        discovered[key] = new PreferenceData(convertedValue, category, isActive, source);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Error parsing Mac plist JSON: {e.Message}");
        }
    }
#endif

#if UNITY_EDITOR_LINUX
    private Dictionary<string, PreferenceData> DiscoverLinuxEditorPrefs()
    {
        var discovered = new Dictionary<string, PreferenceData>();
        
        try
        {
            string prefsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                                          ".config/unity3d/prefs");
            
            if (File.Exists(prefsPath))
            {
                string[] lines = File.ReadAllLines(prefsPath);
                foreach (string line in lines)
                {
                    if (line.Contains(":"))
                    {
                        string[] parts = line.Split(':');
                        if (parts.Length >= 2)
                        {
                            string key = parts[0].Trim();
                            string value = parts[1].Trim();
                            
                            bool isActive = EditorPrefs.HasKey(key);
                            if (!discovered.ContainsKey(key))
                            {
                                object convertedValue = ConvertStringToUnityType(key, value, true);
                                string category = CategorizeEditorPref(key);
                                string source = "Linux Config";
                                
                                discovered[key] = new PreferenceData(convertedValue, category, isActive, source);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Could not read Linux EditorPrefs: {e.Message}");
        }
        
        return discovered;
    }

    private Dictionary<string, PreferenceData> DiscoverLinuxPlayerPrefs()
    {
        var discovered = new Dictionary<string, PreferenceData>();
        
        try
        {
            string companyName = Application.companyName;
            string productName = Application.productName;
            
            string prefsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config/unity3d",
                companyName,
                productName,
                "prefs"
            );
            
            if (File.Exists(prefsPath))
            {
                string[] lines = File.ReadAllLines(prefsPath);
                foreach (string line in lines)
                {
                    if (line.Contains("<pref name="))
                    {
                        var match = Regex.Match(line, @"<pref name=""([^""]+)"" type=""([^""]+)"">([^<]*)</pref>");
                        if (match.Success)
                        {
                            string key = match.Groups[1].Value;
                            string type = match.Groups[2].Value;
                            string valueStr = match.Groups[3].Value;
                            
                            bool isActive = PlayerPrefs.HasKey(key);
                            if (!discovered.ContainsKey(key))
                            {
                                object value = ParseLinuxPrefValue(valueStr, type);
                                string category = CategorizePlayerPref(key);
                                string source = "Linux Config";
                                
                                discovered[key] = new PreferenceData(value, category, isActive, source);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Could not read Linux PlayerPrefs: {e.Message}");
        }
        
        return discovered;
    }

    private object ParseLinuxPrefValue(string valueStr, string type)
    {
        try
        {
            switch (type.ToLower())
            {
                case "int":
                    return int.Parse(valueStr);
                case "float":
                    return float.Parse(valueStr);
                case "string":
                    return valueStr;
                default:
                    return valueStr;
            }
        }
        catch
        {
            return valueStr;
        }
    }
#endif

    private object ConvertRegistryValueToUnityType(string keyName, object registryValue, bool isEditorPref)
    {
        if (registryValue == null) return "";

        switch (registryValue)
        {
            case int intVal:
                return intVal;
            case float floatVal:
                return floatVal;
            case string stringVal:
                if (int.TryParse(stringVal, out int parsedInt))
                    return parsedInt;
                if (float.TryParse(stringVal, out float parsedFloat))
                    return parsedFloat;
                if (bool.TryParse(stringVal, out bool parsedBool))
                    return parsedBool;
                return stringVal;
            case byte[] byteArray:
                return System.Text.Encoding.UTF8.GetString(byteArray);
            default:
                return registryValue.ToString();
        }
    }

    #endregion

    private string GetValueTypeString(object value)
    {
        if (value is int) return "int";
        if (value is float) return "float";
        if (value is bool) return "bool";
        return "string";
    }

    private Color GetTypeColor(string type)
    {
        switch (type)
        {
            case "int": return Color.cyan;
            case "float": return Color.yellow;
            case "bool": return Color.green;
            default: return Color.white;
        }
    }
}
#endif