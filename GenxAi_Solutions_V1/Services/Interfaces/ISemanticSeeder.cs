namespace GenxAi_Solutions_V1.Services.Interfaces
{
    public interface ISemanticSeeder
    {
        //Task RunSeedAsync(int companyId, CancellationToken ct);
        Task RunSeedAsyncNew(int companyId, CancellationToken ct);
        //Task RunSeedPDFAsync(int companyId, CancellationToken ct);
        Task RunSeedPDFAsync_New(int companyId, CancellationToken ct);

    }
}
