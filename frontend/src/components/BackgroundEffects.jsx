import React from 'react';

export default function BackgroundEffects() {
  return (
    <div className="fixed inset-0 z-[-1] overflow-hidden bg-[#FAFAFA]">
      {/* Top Left: Tech Blue (Maintains the digital/software feel) */}
      <div 
        className="absolute -top-[10%] -left-[10%] w-[300px] md:w-[500px] h-[300px] md:h-[500px] bg-blue-200/40 rounded-full blur-[90px] md:blur-[120px] animate-pulse" 
        style={{ animationDuration: '5s' }} 
      />
      
      {/* Bottom Left: Zanvar Red Accent (Replaces the old orange) */}
      <div 
        className="absolute top-[50%] -left-[10%] w-[250px] md:w-[400px] h-[250px] md:h-[400px] bg-red-200/30 rounded-full blur-[80px] md:blur-[100px] animate-pulse" 
        style={{ animationDuration: '7s' }} 
      />
      
      {/* Top Right: Clean Industrial Slate (Replaces the mint green) */}
      <div 
        className="absolute top-[10%] -right-[10%] w-[350px] md:w-[600px] h-[350px] md:h-[600px] bg-slate-200/60 rounded-full blur-[100px] md:blur-[120px] animate-pulse" 
        style={{ animationDuration: '8s' }}
      />
      
      {/* Bottom Right: Soft Blue/Gray mix */}
      <div 
        className="absolute -bottom-[10%] -right-[5%] w-[300px] md:w-[500px] h-[300px] md:h-[500px] bg-sky-100/40 rounded-full blur-[90px] md:blur-[100px] animate-pulse" 
        style={{ animationDuration: '6s' }} 
      />
    </div>
  );
}