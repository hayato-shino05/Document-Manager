using StudyDocumentManager.Core.Entities;
using StudyDocumentManager.Core.Interfaces;
using StudyDocumentManager.Data.Helpers;

namespace StudyDocumentManager.Data.Repositories;

public class SavedSearchRepository : ISavedSearchRepository
{
    private readonly DatabaseHelper _db;

    public SavedSearchRepository(DatabaseHelper db) => _db = db;

    public List<SavedSearch> GetAll() => _db.GetSavedSearches();

    public SavedSearch? GetById(int id) => _db.GetSavedSearchById(id);

    public bool NameExists(string name) => _db.SavedSearchNameExists(name);

    public int Add(SavedSearch savedSearch) => _db.InsertSavedSearch(savedSearch);

    public bool Update(SavedSearch savedSearch) => _db.UpdateSavedSearch(savedSearch);

    public bool Delete(int id) => _db.DeleteSavedSearch(id);
}
