---
name: localization-strings
description: Add or change on-screen text in GamblingGnomes. Pulls the Google Sheet first (Import), adds the new keys to every locale's section asset, regenerates LocalizationKeys, then exports CSVs for the user to upload back to the sheet. Use whenever a change introduces or rewords a localized string (a new label, button, notice, prompt), not for the Unity Localization package.
---

# Adding localized text

The Google Sheet is the source of truth. An Import overwrites every section asset with what the sheet holds, so a key added locally and never uploaded is lost on the next Import. Always run the four steps in order, in one change.

Assets:
- Importer: `Assets/Settings/Localization/LocalizationImporter.asset` (`Localization.Import.LocalizationImporter`)
- Sections: `Assets/Settings/Localization/<CODE>/Locale_<CODE>_<Tab>.asset` (tabs `UI`, `Poker`, `Items`, `Hands`; codes `EN`, `VI`), entries in `_entries` (`Key`, `Value`)
- Generated keys: `Assets/Scripts/Game.Runtime/LocalizationKeys.generated.cs` (never hand-edit)
- Export: `LocalizationExport/<Tab>.csv` (columns `key,en,vi`)

Run every step through Unity MCP `Unity_RunCommand`, on the main editor, not in Play mode (compiling waits for Play mode to end).

## 1. Import the sheet

Pull the latest strings before touching anything, so the export in step 4 does not roll back someone else's sheet edits.

```csharp
var imp = AssetDatabase.LoadAssetAtPath<Localization.Import.LocalizationImporter>("Assets/Settings/Localization/LocalizationImporter.asset");
_ = imp.ImportAsync();
```

`ImportAsync` is async: wait, then read the console for `[LocalizationImporter] Imported N keys from M tabs.` (or its error) before going on. Import also regenerates `LocalizationKeys.generated.cs`.

Before importing, check `git status` for section assets with uncommitted local keys that are not in the sheet yet. If there are any, export and have them uploaded first; importing would erase them.

## 2. Add the keys

Pick the tab by area (`ui.*` → UI, `poker.*` → Poker, `item.*` → Items, `hand.*` → Hands). Key shape `area.group.name`, lower snake case; a key that equals its parent's name or prefixes another gets a `Key` suffix. Add the key to every locale's section of that tab, through `SerializedObject` (never sed on the YAML; Vietnamese is escaped there):

```csharp
var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path));
var list = so.FindProperty("_entries");
list.arraySize++;
var e = list.GetArrayElementAtIndex(list.arraySize - 1);
e.FindPropertyRelative("Key").stringValue = key;
e.FindPropertyRelative("Value").stringValue = value;
so.ApplyModifiedPropertiesWithoutUndo();
AssetDatabase.SaveAssetIfDirty(so.targetObject);
```

To reword an existing key, find its entry and set `Value` the same way.

Text conventions:
- Keywords to stand out: `<Highlight>…</Highlight>` (expanded by `UITextHighlighter`), never a hand `<color>`.
- Runtime values: `{0}`, `{1}`, filled by `Localizer.Format(key, args)`. A label filled this way is set from code, so remove its `LocalizedText`.
- Write the Vietnamese yourself, and tell the user it is your translation.

## 3. Regenerate keys

```csharp
EditorApplication.ExecuteMenuItem("Tools/Localization/Generate Keys");
AssetDatabase.Refresh();
```

Then confirm the new constant is in `LocalizationKeys.generated.cs` and the console has no compile errors. Code reads `LocalizationKeys.X.Y`; a fixed label uses `LocalizedText` with the key picked.

## 4. Export for the sheet

```csharp
imp.ExportToCsv();
```

Check `git diff LocalizationExport/` shows only the intended rows. Tell the user which `LocalizationExport/<Tab>.csv` files changed and that they must upload them to the matching sheet tab; until they do, the next Import erases the new keys.
