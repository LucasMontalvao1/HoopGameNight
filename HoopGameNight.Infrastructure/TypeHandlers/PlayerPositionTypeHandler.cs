using System;
using System.Data;
using Dapper;
using HoopGameNight.Core.Enums;

namespace HoopGameNight.Infrastructure.TypeHandlers
{
    /// <summary>
    /// TypeHandler do Dapper para conversão entre o tipo ENUM (string) do MySQL e o enum <see cref="PlayerPosition"/>.
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

            // Realiza o parse da string para o tipo enum ignorando diferenciação de maiúsculas/minúsculas
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
    /// TypeHandler do Dapper para PlayerPosition não-anulável, utilizado em campos obrigatórios do esquema.
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
