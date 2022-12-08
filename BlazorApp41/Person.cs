namespace BlazorApp41;

public class Person
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public string? Salutation { get; set; }
	public string? GivenName { get; set; }
	public string? FamilyName { get; set; }
}
