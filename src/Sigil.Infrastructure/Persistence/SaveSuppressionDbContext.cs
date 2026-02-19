using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Sigil.Domain.Entities;

namespace Sigil.infrastructure.Persistence;

internal class SaveSuppressionDbContext<T>(DbContextOptions<T> options): IdentityDbContext<User, IdentityRole<Guid>, Guid>(options) where T : DbContext
{
    private readonly SaveSuppressionManager _suppressionManager = new ();

    public bool IsSaveSuppressed => _suppressionManager.IsSuppressed;
    
    public IDisposable SuppressSave() => _suppressionManager.SuppressSave();
    
    public override int SaveChanges() => _suppressionManager.IsSuppressed ? 0 : base.SaveChanges();
    
    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        _suppressionManager.IsSuppressed 
            ? 0 
            : base.SaveChanges(acceptAllChangesOnSuccess);

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _suppressionManager.IsSuppressed 
            ? Task.FromResult(0) 
            : base.SaveChangesAsync(cancellationToken);

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        _suppressionManager.IsSuppressed 
            ? Task.FromResult(0) 
            : base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

    public int ForceSaveChanges() => base.SaveChanges();
    
    public Task<int> ForceSaveChangesAsync(CancellationToken cancellationToken = default) => base.SaveChangesAsync(cancellationToken);

    
    public override async ValueTask DisposeAsync()
    {
        _suppressionManager.Reset();
        await base.DisposeAsync();
    }

    public override void Dispose()
    {
        _suppressionManager.Reset();
        base.Dispose();
    }
}