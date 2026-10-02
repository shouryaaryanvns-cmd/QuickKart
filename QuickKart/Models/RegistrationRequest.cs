using System;

namespace QuickKart.Models
{
    public sealed class RegistrationRequest
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string Password { get; set; }
    }
}
