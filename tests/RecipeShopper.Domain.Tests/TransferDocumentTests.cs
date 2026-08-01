using RecipeShopper.Application.Models.ImportExport;

namespace RecipeShopper.Domain.Tests;

public sealed class TransferDocumentTests
{
    [Fact]
    public void CatalogDocument_DefaultsToCurrentPortableContract()
    {
        var document = new CatalogDocumentDto();

        Assert.Equal(TransferSchema.CurrentVersion, document.SchemaVersion);
        Assert.Equal(TransferSchema.CatalogDocumentType, document.DocumentType);
        Assert.NotNull(document.Categories);
        Assert.NotNull(document.Recipes);
        Assert.NotNull(document.Ingredients);
    }

    [Fact]
    public void BackupDocument_DefaultsToBackupContractAndContainsCatalog()
    {
        var document = new BackupDocumentDto();

        Assert.Equal(TransferSchema.BackupDocumentType, document.DocumentType);
        Assert.Equal(TransferSchema.CurrentVersion, document.SchemaVersion);
        Assert.NotNull(document.Catalog);
        Assert.NotNull(document.Images);
    }
}
