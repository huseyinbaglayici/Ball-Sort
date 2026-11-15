namespace Project.Contexts.Game.Items
{
    /// <summary>
    /// Everything the board can spawn. The values are serialized in the item registry, so never renumber them.
    /// </summary>
    public enum ItemType
    {
        None = 0,
        Bottle = 1,
        Ball = 2,
        QuestionMark = 3
    }
}
