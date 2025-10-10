namespace MartenPlayground.Users.Domain;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Phone { get; set; }
    public string Email { get; set; }
    public string City { get; set; }
    public string Country { get; set; }
    public Address Address { get; set; }
}

public class Address
{
    public string AddressLine1 { get; set; }
    public string AddressLine2 { get; set; }

    public string Zip { get; set; }
}
