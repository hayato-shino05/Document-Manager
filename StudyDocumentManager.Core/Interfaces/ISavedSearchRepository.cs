using StudyDocumentManager.Core.Entities;

namespace StudyDocumentManager.Core.Interfaces;

public interface ISavedSearchRepository
{
    List<SavedSearch> GetAll();
    SavedSearch? GetById(int id);
    bool NameExists(string name);
    int Add(SavedSearch savedSearch);
    bool Update(SavedSearch savedSearch);
    bool Delete(int id);
}
