using Microsoft.AspNetCore.Identity;

namespace PetPal.Api.Entities;

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }

    public ApplicationRole(string name) : base(name) { }
}
