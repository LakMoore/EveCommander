namespace BotLib
{
  public record BotCharacter
  {
    public required string Name { get; set; }
    public required string Location { get; set; }
    public bool? IsAlphaClone { get; set; }
  }
}
