namespace YogaClassManager.Models.Passes
{
    public class PassAlteration
    {
        public int Id { get; private set; }
        public int PassId { get; set; }
        public int Amount { get; set; }
        public string Reason { get; set; }

        public PassAlteration(int id, int passId, int amount, string reason)
        {
            Id = id;
            PassId = passId;
            Amount = amount;
            Reason = reason;
        }

        public bool IsValid()
        {
            return Amount != 0;
        }

        internal static PassAlteration Copy(PassAlteration alteration)
        {
            return new PassAlteration(alteration.Id, alteration.PassId, alteration.Amount, alteration.Reason);
        }
    }
}
