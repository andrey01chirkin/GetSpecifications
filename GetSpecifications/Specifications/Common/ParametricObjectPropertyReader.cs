using System.Globalization;
using Multicad;
using Multicad.Objects;

namespace GetSpecifications.Specifications.Common
{
    /// <summary>Снимок параметров одного параметрического объекта СПДС, нужных для спецификаций.</summary>
    public sealed class ParametricObjectData
    {
        public string ObjectName { get; set; } = "";
        public string Implementation { get; set; } = "";
        public string TipStoiki { get; set; } = "";
        public string TipFundamenta { get; set; } = "";
        public string TipTraversy { get; set; } = "";
        public double DlinaStoikiM { get; set; }
    }

    /// <summary>
    /// Считывает нужные параметры из ObjectProperties за один проход
    /// </summary>
    public sealed class ParametricObjectPropertyReader
    {
        public ParametricObjectData Read(McParametricObject obj)
        {
            var data = new ParametricObjectData
            {
                ObjectName = obj.Name ?? ""
            };

            foreach (McProperty prop in obj.DbEntity.ObjectProperties)
            {
                object? val = prop.GetValue(obj);

                switch (prop.Name)
                {
                    case SpecificationObjectConstants.PARAM_IMPL:
                        data.Implementation = val?.ToString() ?? "";
                        break;

                    case SpecificationObjectConstants.PARAM_TIP_STOIKI:
                        data.TipStoiki = val?.ToString() ?? "";
                        break;

                    case SpecificationObjectConstants.PARAM_TIP_FUND:
                        data.TipFundamenta = val?.ToString() ?? "";
                        break;

                    case SpecificationObjectConstants.PARAM_TIP_TRAV:
                        data.TipTraversy = val?.ToString() ?? "";
                        break;

                    case SpecificationObjectConstants.PARAM_DLINA:
                        // Параметр может приходить как double или как строка в зависимости от версии скрипта.
                        if (val is double d)
                            data.DlinaStoikiM = d;
                        else if (double.TryParse(
                            val?.ToString(),
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out double parsed))
                            data.DlinaStoikiM = parsed;
                        break;
                }
            }

            return data;
        }
    }
}