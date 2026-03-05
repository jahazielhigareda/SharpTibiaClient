using OpenTibia.Data.Models;

namespace OpenTibia.Data.Repositories
{
    public interface IRuleViolationRepository
    {
        void AddRuleViolation(DbRuleViolation ruleViolation);
    }
}