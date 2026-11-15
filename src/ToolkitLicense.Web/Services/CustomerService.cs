using Microsoft.EntityFrameworkCore;
using ToolkitLicense.Web.Data;
using ToolkitLicense.Web.Domain;

namespace ToolkitLicense.Web.Services;

public sealed record CustomerInput(string? Name, string? Email, string? Note);

/// <summary>The people who use Toolkit. Deleting one keeps their devices and licenses, unassigned.</summary>
public sealed class CustomerService(LicenseDbContext db, IAuditLog audit)
{
    public Task<List<Customer>> AllAsync() =>
        db.Customers.Include(x => x.Devices).OrderBy(x => x.Name).ToListAsync();

    public Task<Customer?> GetAsync(int id) =>
        db.Customers.Include(x => x.Devices).FirstOrDefaultAsync(x => x.Id == id);

    public async Task<string?> CreateAsync(CustomerInput input)
    {
        var error = Validate(input);
        if (error is not null)
        {
            return error;
        }

        var customer = new Customer();
        Apply(customer, input);
        db.Customers.Add(customer);
        audit.Add(AuditActions.CustomerCreated, customer.Name);
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> UpdateAsync(int id, CustomerInput input)
    {
        var customer = await db.Customers.FindAsync(id);
        if (customer is null)
        {
            return "Customer.Error.NotFound";
        }

        var error = Validate(input);
        if (error is not null)
        {
            return error;
        }

        Apply(customer, input);
        audit.Add(AuditActions.CustomerUpdated, customer.Name);
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> DeleteAsync(int id)
    {
        var customer = await db.Customers.FindAsync(id);
        if (customer is null)
        {
            return "Customer.Error.NotFound";
        }

        db.Customers.Remove(customer);
        audit.Add(AuditActions.CustomerDeleted, customer.Name);
        await db.SaveChangesAsync();
        return null;
    }

    private static string? Validate(CustomerInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            return "Customer.Error.Name";
        }

        if (!string.IsNullOrWhiteSpace(input.Email) && !input.Email.Contains('@'))
        {
            return "Customer.Error.Email";
        }

        return null;
    }

    private static void Apply(Customer customer, CustomerInput input)
    {
        customer.Name = DeviceTokens.Clip(input.Name, 120);
        customer.Email = string.IsNullOrWhiteSpace(input.Email) ? null : DeviceTokens.Clip(input.Email, 200);
        customer.Note = string.IsNullOrWhiteSpace(input.Note) ? null : DeviceTokens.Clip(input.Note, 500);
    }
}
