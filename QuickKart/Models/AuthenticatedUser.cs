namespace QuickKart.Models
{
    public sealed class AuthenticatedUser
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string LoginName { get; set; }
        public string Role { get; set; }
    }
}
