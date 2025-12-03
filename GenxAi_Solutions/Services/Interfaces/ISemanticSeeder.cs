namespace GenxAi_Solutions.Services.Interfaces
{
    public interface ISemanticSeeder
    {
        Task RunSeedAsync(int companyId, CancellationToken ct);
        Task RunSeedPDFAsync(int companyId, CancellationToken ct);

    }
}
