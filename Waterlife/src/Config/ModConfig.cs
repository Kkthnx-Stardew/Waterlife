namespace Waterlife;

public sealed class ModConfig
{
    public bool DrawFishShadows { get; set; } = true;

    public int MaximumFish { get; set; } = 160;

    public float FishDensity { get; set; } = 0.1f;

    public bool TrashOutside { get; set; } = true;

    public bool TrashFarm { get; set; } = true;

    public bool DisableTrashLvl10 { get; set; } = true;

    public bool DrawItemShadows { get; set; } = true;

    public bool ShowTreasure { get; set; } = true;

    public bool RotateAlgae { get; set; }

    public bool FilterEels { get; set; }
}
