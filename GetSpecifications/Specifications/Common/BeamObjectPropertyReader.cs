using System.Globalization;
using Multicad;
using Multicad.Objects;

namespace GetSpecifications.Specifications.Common
{
    /// <summary>Снимок параметров одного параметрического объекта балки.</summary>
    public sealed class BeamObjectData
    {
        public string ObjectName { get; set; } = "";
        public string SName { get; set; } = "";
        public double RL { get; set; }
        public double NormNagruzka { get; set; }
        public double RaschNagruzka { get; set; }
        public string PokrytieEstakady { get; set; } = "";
        // Длина траверсы в метрах (rDlinaTraversy) — нужна для расчёта поз. «г» таблицы №4.
        public double DlinaTraversyM { get; set; }
    }

    /// <summary>Считывает параметры балки из ObjectProperties за один проход.</summary>
    public sealed class BeamObjectPropertyReader
    {
        public BeamObjectData Read(McParametricObject obj)
        {
            var data = new BeamObjectData { ObjectName = obj.Name ?? "" };

            foreach (McProperty prop in obj.DbEntity.ObjectProperties)
            {
                object? val = prop.GetValue(obj);

                switch (prop.Name)
                {
                    case SpecificationObjectConstants.PARAM_SNAME:
                        data.SName = val?.ToString() ?? "";
                        break;

                    case SpecificationObjectConstants.PARAM_RL:
                        if (val is double rl) data.RL = rl;
                        else if (double.TryParse(val?.ToString(), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out double prl)) data.RL = prl;
                        break;

                    case SpecificationObjectConstants.PARAM_NORM_NAGRUZKA:
                        if (val is double nn) data.NormNagruzka = nn;
                        else if (double.TryParse(val?.ToString(), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out double pnn)) data.NormNagruzka = pnn;
                        break;

                    case SpecificationObjectConstants.PARAM_RASCH_NAGRUZKA:
                        if (val is double rn) data.RaschNagruzka = rn;
                        else if (double.TryParse(val?.ToString(), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out double prn)) data.RaschNagruzka = prn;
                        break;

                    case SpecificationObjectConstants.PARAM_POKRYTIE:
                        data.PokrytieEstakady = val?.ToString() ?? "";
                        break;

                    case SpecificationObjectConstants.PARAM_DLINA_TRAVERSY:
                        if (val is double dt) data.DlinaTraversyM = dt;
                        else if (double.TryParse(val?.ToString(), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out double pdt)) data.DlinaTraversyM = pdt;
                        break;
                }
            }

            return data;
        }
    }
}