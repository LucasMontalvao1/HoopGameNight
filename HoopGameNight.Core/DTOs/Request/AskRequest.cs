using System.ComponentModel.DataAnnotations;

namespace HoopGameNight.Core.DTOs.Request
{
    public class AskRequest
    {
        [Required(ErrorMessage = "A pergunta é obrigatória")]
        [MinLength(5, ErrorMessage = "A pergunta deve ter pelo menos 5 caracteres")]
        [MaxLength(500, ErrorMessage = "A pergunta não pode ter mais de 500 caracteres")]
        public string Question { get; set; } = string.Empty;
    }
}