using OpenTibia.Data.Contexts;
using OpenTibia.Data.Models;

namespace OpenTibia.Data.Repositories
{
    public class RuleViolationRepository : IRuleViolationRepository
    {
        private DatabaseContext context;

        public RuleViolationRepository(DatabaseContext context)
        {
            this.context = context;
        }

        public void AddRuleViolation(DbRuleViolation ruleViolation)
        {
            context.RuleViolations.Add(ruleViolation);
        }
    }    
}