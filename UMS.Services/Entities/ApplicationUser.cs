using Microsoft.AspNetCore.Identity;

namespace UMS.Services.Entities;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = "";
    public string MiddleName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string CitizenIdentityCardNumber { get; set; } = "";
    public DateTime DateOfBirth { get; set; } = DateTime.Now;
    public SexEnum Sex { get; set; }
    public string Country { get; set; } = "";
    public string Nationality { get; set; } = "";
    public string City { get; set; } = "";
    public string Address { get; set; } = "";
    public string DepartmentId { get; set; } = "";
}

public enum SexEnum
{
    Male,
    Female,
    Other
}

