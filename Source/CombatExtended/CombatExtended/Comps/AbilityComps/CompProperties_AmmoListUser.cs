using System.Collections.Generic;
using System.Xml;
using Verse;

namespace CombatExtended;

public class CompProperties_AmmoListUser : CompProperties_AmmoUser
{
    public List<AmmoSetDef> additionalAmmoSets = new List<AmmoSetDef>();
    public List<AmmoSpawnOption> ammoSpawnOptions = new List<AmmoSpawnOption>();

    public CompProperties_AmmoListUser()
    {
        compClass = typeof(CompAmmoListUser);
    }

    public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
    {
        foreach (string error in base.ConfigErrors(parentDef))
        {
            yield return error;
        }

        if (ammoSpawnOptions.NullOrEmpty())
        {
            yield break;
        }

        foreach (AmmoSpawnOption spawnConfig in ammoSpawnOptions)
        {
            bool valid = (ammoSet != null && ammoSet.ammoTypes.Any(x => x.ammo == spawnConfig.ammoDef)) || additionalAmmoSets.Any(set => set.ammoTypes.Any(x => x.ammo == spawnConfig.ammoDef));

            if (!valid)
            {
                yield return $"CE : {parentDef.defName} has incorrect ammoDef {spawnConfig.ammoDef.defName} in CompProperties_AmmoList.ammoSpawnOptions. The ammoDef needs to be included as part of either ammoSet or additionalAmmoSets.";
            }
        }
    }

}
public class AmmoSpawnOption
{
    public FloatRange percentRange = new FloatRange(1, 1);
    public AmmoDef ammoDef;
    public float weight = 1f;

    public void LoadDataFromXmlCustom(XmlNode xmlRoot)
    {
        foreach (XmlNode node in xmlRoot.ChildNodes)
        {
            if (node.Name == "ammoDef")
            {
                DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, "ammoDef", node.InnerText);
            }
            else if (node.Name == "percentRange")
            {
                if (float.TryParse(node.InnerText, out float single))
                {
                    percentRange = new FloatRange(single, single);
                }
                else
                {
                    percentRange = FloatRange.FromString(node.InnerText);
                }
            }
            else if (node.Name == "weight")
            {
                weight = ParseHelper.FromString<float>(node.InnerText);
            }
        }
    }
}
