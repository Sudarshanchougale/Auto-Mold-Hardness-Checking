import React from 'react';
import { Play, BarChart2, ShieldCheck, Settings, Zap, TrendingUp } from 'lucide-react';

export default function Hero() {
  return (
    <div className="max-w-7xl mx-auto px-6 pt-28 pb-8 lg:pt-32 lg:pb-10">
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-12 items-center">
        
        {/* Left Column: Hero Content */}
        <div className="flex flex-col z-10 max-w-3xl">
          <div className="inline-flex items-center gap-2 px-3 py-1.5 rounded-full bg-red-5
          
          
          
          
          
          0 border border-red-500 text-red-600 text-xs font-bold w-fit mb-6">
            <Settings className="w-3.5 h-3.5" />
            Sand Mold Hardness Checking
          </div>
          
          <h1 className="text-5xl lg:text-7xl font-black text-zanvar-dark tracking-tight leading-[1.1] mb-6">
            Automated Mold <br />
            <span className="text-transparent bg-clip-text bg-gradient-to-r from-zanvar-red to-red-500">
              Hardness
            </span> Inspection
          </h1>
          
          <p className="text-lg text-gray-600 mb-10 max-w-lg leading-relaxed">
            A 3D simulation to ensure stronger molds, less defects, and higher productivity — built for the future of smart foundries.
          </p>
          
          <div className="flex flex-wrap items-center gap-4 mb-14">
            <button className="flex items-center gap-2 bg-zanvar-red hover:bg-red-700 text-white font-bold py-3.5 px-8 rounded-full transition-all hover:-translate-y-0.5">
              <Play className="w-4 h-4" /> Try Simulation
            </button>
            <button className="flex items-center gap-2 bg-zanvar-red hover:bg-red-700 text-white font-bold py-3.5 px-8 rounded-full transition-all border border-red-500/30 shadow-sm hover:-translate-y-0.5">
              <BarChart2 className="w-4 h-4 text-white" /> View Dashboard
            </button>
          </div>
          
          {/* Feature Mini-Icons */}
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-6 pt-6 border-t border-gray-400/50">
            {[
              { icon: ShieldCheck, color: 'text-green-500', label: 'Detect Defects Early' },
              { icon: Settings, color: 'text-blue-500', label: 'Reduce Scrap & Time' },
              { icon: Zap, color: 'text-yellow-500', label: 'Save Energy & Resources' },
              { icon: TrendingUp, color: 'text-purple-500', label: 'Improve Productivity' }
            ].map((feat, i) => (
              <div key={i} className="flex flex-col gap-2 group cursor-pointer">
                <feat.icon className={`w-6 h-6 ${feat.color} group-hover:-translate-y-1 transition-transform`} />
                <span className="text-[11px] font-bold text-gray-500 leading-tight">{feat.label}</span>
              </div>
            ))}
          </div>
        </div>

        {/* Right Column: Video Showcase */}
        <div className="relative w-full h-[400px] sm:h-[450px] lg:h-[550px] rounded-[2rem] p-2 lg:p-3 bg-white/40 border border-black/20 shadow-2xl backdrop-blur-sm group flex items-center justify-center mt-10 lg:mt-0">
          
          {/* Subtle background glow */}
          <div className="absolute inset-0 bg-gradient-to-tr from-zanvar-red/10 to-transparent rounded-[2rem] pointer-events-none" />
          
          {/* Video Element */}
          <video 
            autoPlay 
            loop 
            muted 
            playsInline
            className="w-full h-full object-cover rounded-[1.5rem] shadow-inner"
          >
            <source src="/bg-video.mp4" type="video/mp4" />
            Your browser does not support the video tag.
          </video>
          
          {/* Bottom Left Decorative Blur */}
          <div className="absolute -bottom-6 -left-6 w-32 h-32 bg-zanvar-red/20 blur-3xl rounded-full z-[-1]" />
        </div>
        
      </div>
    </div>
  );
}