using System.ComponentModel.DataAnnotations;
namespace HoopGameNight.Core.DTOs.Request
{
    /// <summary>
    /// Requisição para buscar jogos de múltiplos times em um período
    /// </summary>
    public class GetMultipleTeamsGamesRequest : BaseRequest
    {
        /// <summary>
        /// Lista de IDs dos times para buscar jogos
        /// </summary>
        [Required(ErrorMessage = "Os IDs dos times são obrigatórios")]
        [MinLength(1, ErrorMessage = "Pelo menos um ID de time é obrigatório")]
        [MaxLength(10, ErrorMessage = "Máximo de 10 times permitidos")]
        public List<int> TeamIds { get; set; } = new();

        /// <summary>
        /// Data inicial do período
        /// </summary>
        [Required(ErrorMessage = "A data inicial é obrigatória")]
        public DateTime StartDate { get; set; }

        /// <summary>
        /// Data final do período
        /// </summary>
        [Required(ErrorMessage = "A data final é obrigatória")]
        public DateTime EndDate { get; set; }

        /// <summary>
        /// Se deve agrupar os jogos por time
        /// </summary>
        public bool GroupByTeam { get; set; } = true;

        /// <summary>
        /// Se deve incluir apenas jogos futuros
        /// </summary>
        public bool OnlyFutureGames { get; set; } = false;

        /// <summary>
        /// Se deve incluir estatísticas no retorno
        /// </summary>
        public bool IncludeStats { get; set; } = false;

        /// <summary>
        /// Valida se a requisição está correta
        /// </summary>
        public override bool IsValid()
        {
            if (TeamIds == null || TeamIds.Count == 0)
                return false;

            if (StartDate > EndDate)
                return false;

            // Máximo de 30 dias de intervalo para evitar sobrecarga
            if ((EndDate - StartDate).TotalDays > 30)
                return false;

            // Não permitir datas muito antigas (mais de 2 anos)
            if (StartDate < DateTime.Today.AddYears(-2))
                return false;

            // Não permitir datas muito futuras (mais de 1 ano)
            if (EndDate > DateTime.Today.AddYears(1))
                return false;

            // Verificar IDs válidos
            if (TeamIds.Any(id => id <= 0))
                return false;

            return true;
        }

        /// <summary>
        /// Retorna mensagens de erro de validação
        /// </summary>
        public List<string> GetValidationErrors()
        {
            var errors = new List<string>();

            if (TeamIds == null || TeamIds.Count == 0)
                errors.Add("Pelo menos um ID de time é obrigatório");

            if (StartDate > EndDate)
                errors.Add("A data inicial deve ser anterior à data final");

            if ((EndDate - StartDate).TotalDays > 30)
                errors.Add("O intervalo de datas não pode exceder 30 dias");

            if (StartDate < DateTime.Today.AddYears(-2))
                errors.Add("A data inicial não pode ser mais de 2 anos no passado");

            if (EndDate > DateTime.Today.AddYears(1))
                errors.Add("A data final não pode ser mais de 1 ano no futuro");

            if (TeamIds?.Any(id => id <= 0) == true)
                errors.Add("Todos os IDs dos times devem ser maiores que zero");

            return errors;
        }
    }
}