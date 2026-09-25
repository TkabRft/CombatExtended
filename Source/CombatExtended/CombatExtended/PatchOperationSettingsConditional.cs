using System.Xml;
using HarmonyLib;
using Verse;

namespace CombatExtended;

public class PatchOperationSettingsConditional : PatchOperation
{
    public PatchOperation match;
    public PatchOperation nomatch;
    public string settingName;

    public override bool ApplyWorker(XmlDocument xml)
    {
        var settingRef = AccessTools.Field(typeof(Settings), settingName);
        if (settingRef == null)
        {
            Log.Error($"[Combat Extended] Cannot find the settings field named {settingName}");
            return false;
        }

        if (settingRef.FieldType != typeof(bool))
        {
            Log.Error($"[Combat Extended] Setting field named {settingName} is not a bool");
            return false;
        }

        if ((bool)settingRef.GetValue(Controller.settings)) //1-to-1 copy of vanilla conditional patch op
        {
            if (match != null)
            {
                return match.Apply(xml);
            }
        }
        else if (nomatch != null)
        {
            return nomatch.Apply(xml);
        }
        if (match == null)
        {
            return nomatch != null;
        }
        return true;
    }
}
