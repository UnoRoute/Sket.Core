namespace Bracketcore.Sket.Model
{
    public abstract class SketEmailModel: SketPersistedModel
    {
        public string To { get; set; }
        public string From { get; set; }
        public string Text { get; set; }
        public string Subject { get; set; }
        public string Html { get; set; }
    }
}