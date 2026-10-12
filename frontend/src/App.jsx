import React from 'react';
import BackgroundEffects from './components/BackgroundEffects';
import Navbar from './components/Navbar';
import Hero from './components/Hero';

function App() {
  return (
    <div className="relative min-h-screen overflow-x-hidden font-sans">
      <BackgroundEffects />
      <Navbar />
      
      {/* Main Content wrapper with top padding to clear fixed navbar */}
      <main>
        <Hero />
      </main>
    </div>
  );
}

export default App;