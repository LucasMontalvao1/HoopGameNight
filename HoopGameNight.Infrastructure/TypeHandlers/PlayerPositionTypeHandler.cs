using Dapper;
using HoopGameNight.Core.Enums;
using System.Data;

namespace HoopGameNight.Infrastructure.TypeHandlers
{
    /// <summary>
    /// TypeHandler para converter string do banco MySQL ENUM para PlayerPosition C# enum
    /// </summary>
    public class PlayerPositionTypeHandler : SqlMapper.TypeHandler<PlayerPosition?>
    {
        public override PlayerPosition? Parse(object value)
        {
            if (value == null || value == DBNull.Value)
                return null;

            var stringValue = value.ToString();
            if (string.IsNullOrWhiteSpace(stringValue))
                return null;

            // Tentar fazer parse da string para enum
            if (Enum.TryParse<PlayerPosition>(stringValue, ignoreCase: true, out var position))
            {
                return position;
            }

            // Caso não consiga, retornar null
            return null;
        }

        public override void SetValue(IDbDataParameter parameter, PlayerPosition? value)
        {
            if (value.HasValue)
            {
                parameter.Value = value.Value.ToString();
            }
            else
            {
                parameter.Value = DBNull.Value;
            }
        }
    }

    /// <summary>
    /// TypeHandler para PlayerPosition não nullable (para casos onde é obrigatório)
    /// </summary>
    public class PlayerPositionNonNullableTypeHandler : SqlMapper.TypeHandler<PlayerPosition>
    {
        public override PlayerPosition Parse(object value)
        {
            if (value == null || value == DBNull.Value)
                return PlayerPosition.PG; // Valor padrão

            var stringValue = value.ToString();
            if (string.IsNullOrWhiteSpace(stringValue))
                return PlayerPosition.PG;

            if (Enum.TryParse<PlayerPosition>(stringValue, ignoreCase: true, out var position))
            {
                return position;
            }

            return PlayerPosition.PG;
        }

        public override void SetValue(IDbDataParameter parameter, PlayerPosition value)
        {
            parameter.Value = value.ToString();
        }
    }
}
