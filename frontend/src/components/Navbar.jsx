import React from 'react';

export default function Navbar() {
  return (
    <div className="fixed top-6 left-0 w-full z-50 px-4 md:px-8 flex justify-center pointer-events-none">
      {/* Fixed wrapper to center the floating pill */}
      
      {/* The Floating Pill Navbar */}
      <nav className="pointer-events-auto w-full max-w-7xl h-20 px-4 md:px-8 rounded-full flex items-center justify-between bg-gradient-to-r from-white/70 via-white/40 to-blue-50/50 backdrop-blur-2xl border border-black/20 shadow-[0_12px_40px_-12px_rgba(0,0,0,0.15)] relative overflow-hidden">
        
        {/* Subtle inner colorful glow to match the reference */}
        <div className="absolute inset-0 bg-gradient-to-r from-pink-100/20 via-transparent to-purple-100/20 pointer-events-none" />

        {/* Left: Logo */}
        <div className="relative flex items-center gap-3 cursor-pointer group z-10">
          {/* <div className="bg-white p-1.5 rounded-lg shadow-sm border border-gray-100"> */}
            <img 
              src="/zanvar-logo.png" 
              alt="Zanvar Group" 
              className="h-14 object-contain group-hover:scale-105 transition-transform duration-300" 
            />
          {/* </div> */}
          <span className="text-base md:text-lg font-black text-gray-900 tracking-[0.15em] uppercase">
            Zanvar Group
          </span>
        </div>


        {/* Right: Button & Icon */}
        <div className="relative flex items-center gap-3 md:gap-5 z-10">
          <button className="bg-gradient-to-r from-zanvar-red to-rose-500 hover:from-red-600 hover:to-rose-600 text-white text-[10px] md:text-xs font-bold py-3 px-6 md:px-8 rounded-full transition-all duration-300 hover:-translate-y-0.5 uppercase tracking-widest">
            Get Started
          </button>
        </div>
        
      </nav>
    </div>
  );
}