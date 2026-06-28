using System.Collections.Generic;

namespace Foundation.Melpomene
{
    /// <summary>
    /// 送信先共通のラベル組み立て。defaultLabels + priority + category(いずれも小文字)。
    /// GitHub 直送・リレー送信で同一のラベルを使うため切り出している。
    /// </summary>
    public static class MelpomeneLabelBuilder
    {
        public static string[] Build(MelpomeneRuntimeConfig config, MelpomeneReportTicket ticket)
        {
            var labels = new List<string>();
            if (config != null && config.defaultLabels != null)
            {
                labels.AddRange(config.defaultLabels);
            }
            labels.Add(ticket.priority.ToString().ToLower());
            labels.Add(ticket.category.ToString().ToLower());
            return labels.ToArray();
        }
    }
}
