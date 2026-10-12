import React from 'react';
import { Cpu, Database, BellRing, Settings2 } from 'lucide-react';

export default function Features() {
  const platformFeatures = [
    { icon: Cpu, title: 'Robotic Arm Simulation', desc: 'Hybrid articulated arm with controlled vertical probe.' },
    { icon: Settings2, title: 'Interactive + Automated', desc: 'Run simulations or control the arm manually via UI.' },
    { icon: Database, title: 'Data & Dashboard', desc: 'Real-time data, historical trends, and analytics.' },
    { icon: BellRing, title: 'Smart Alerts', desc: 'Instant buzzer/notification on hardness failure.' }
  ];

  return (
    <div className="max-w-7xl mx-auto px-6 pb-20 relative z-10">
      <div className="bg-white/80 backdrop-blur-xl rounded-[2.5rem] border border-white p-8 lg:p-12 shadow-[0_8px_30px_rgb(0,0,0,0.04)]">
        
        {/* Top Platform Features */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-8">
          <div className="lg:col-span-1">
            <p className="text-[10px] font-bold tracking-widest text-zanvar-red uppercase mb-2">Key Features</p>
            <h3 className="text-2xl font-black text-zanvar-dark leading-tight">Everything You Need in One Platform</h3>
          </div>
          <div className="lg:col-span-4 grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-8">
            {platformFeatures.map((f, i) => (
              <div key={i} className="flex flex-col gap-3">
                <div className="w-10 h-10 rounded-full bg-red-50 flex items-center justify-center">
                  <f.icon className="w-5 h-5 text-zanvar-red" />
                </div>
                <h4 className="font-bold text-gray-900 text-sm">{f.title}</h4>
                <p className="text-xs text-gray-500 leading-relaxed">{f.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}