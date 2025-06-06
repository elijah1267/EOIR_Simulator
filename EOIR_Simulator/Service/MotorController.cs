using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EOIR_Simulator.Model;

namespace EOIR_Simulator.Service
{
    public class MotorController //: IDisposable
    {
        private readonly CommandSender _tcp;

        // 얘네 지금 아무짝에도 쓸모 없음.. VM 쪽에서 로직 끌어오자 나중에

        /* ── 두 축 각도 ───────────────────── */
        public int Yaw { get; private set; } = 90;   // 좌/우
        public int Pitch { get; private set; } = 90;   // 상/하
        public event Action<int, int> AngleChanged;    // (yaw,pitch)

        public MotorController(CommandSender tcp) { _tcp = tcp; }

        /* ── Clamp 함수 ───────────────────── */
        private static int Clip(int deg) => deg < 0 ? 0 : deg > 180 ? 180 : deg;

        /* ── 수동 제어 (±5° 단위) ─────────── */
        public async Task ManualMoveAsync(sbyte dx, sbyte dy)
        {
            Yaw = Clip(Yaw + dx);
            Pitch = Clip(Pitch + dy);
            AngleChanged?.Invoke(Yaw, Pitch);
            await _tcp.SendAsync(ModeNum.Manual, dx, dy);
        }
    }
}
