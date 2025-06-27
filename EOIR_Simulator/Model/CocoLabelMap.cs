using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace EOIR_Simulator.Model
{
    public static class CocoLabelMap
    {
        // 읽기 전용 사전
        private static readonly IReadOnlyDictionary<int, string> _labels;

        // 정적 생성자: 앱이 처음 로드될 때 한 번만 실행
        //static CocoLabelMap()
        //{
        //    var dict = new Dictionary<int, string>();

        //    foreach (var raw in File.ReadLines("C:\\workspace_WPF\\EOIR_Simulator_msb\\EOIR_Simulator\\coco_labels.txt"))   // ·· 경로만 맞춰 주세요
        //    {
        //        if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("#")) continue;

        //        // "숫자(첫 번째 공백 전까지)" + "이름(그 뒤 전체)"
        //        int firstSpace = raw.IndexOf(' ');
        //        if (firstSpace < 1) continue;                  // 형식이 틀린 라인 무시

        //        string keyPart = raw.Substring(0, firstSpace);
        //        string valuePart = raw.Substring(firstSpace + 1).Trim();  // 'traffic light' 같은 여러 단어 포함

        //        if (int.TryParse(keyPart, out int id))
        //            dict[id] = valuePart;
        //    }

        //    _labels = new ReadOnlyDictionary<int, string>(dict); // 외부 수정 금지
        //}

        ///// <summary>
        ///// 클래스 번호 → 라벨 문자열
        ///// </summary>
        //public static bool TryGetLabel(int classId, out string label) =>
        //    _labels.TryGetValue(classId, out label);

        static CocoLabelMap()
        {
            var dict = new Dictionary<int, string>();

            foreach (var raw in File.ReadLines("coco_labels.txt"))   // 경로만 맞춰 주세요
            {
                if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("#"))
                    continue;

                int idx = raw.IndexOf(' ');
                if (idx < 1) continue;                          // 잘못된 라인

                string keyPart = raw.Substring(0, idx);
                string valuePart = raw.Substring(idx + 1).Trim();  // 'traffic light' 포함

                int id;
                if (int.TryParse(keyPart, out id))
                    dict[id] = valuePart;
            }

            _labels = new ReadOnlyDictionary<int, string>(dict);
        }

        public static bool TryGet(int id, out string label)
        {
            return _labels.TryGetValue(id, out label);
        }
    }
}
