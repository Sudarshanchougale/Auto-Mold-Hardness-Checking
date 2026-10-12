# Automated Mold Hardness Inspection

A modern, responsive web application for monitoring and simulating sand mold hardness inspection in foundries. Built with React, Vite, and Tailwind CSS, this platform provides real-time visualization and analytics for automated quality control in manufacturing.

![React](https://img.shields.io/badge/React-18.3.1-blue)
![Vite](https://img.shields.io/badge/Vite-8.3.4-purple)
![Tailwind](https://img.shields.io/badge/Tailwind-3.4.0-38bdf8)

## 🎯 Features

- **Real-time Dashboard**: Monitor mold hardness data with live updates
- **3D Simulation**: Interactive robotic arm simulation for hardness testing
- **Smart Alerts**: Instant notifications for hardness failures
- **Responsive Design**: Optimized for desktop, tablet, and mobile devices
- **Modern UI**: Glassmorphism design with smooth animations
- **Data Analytics**: Historical trends and productivity metrics

## 🚀 Tech Stack

- **Frontend Framework**: React 18.3.1
- **Build Tool**: Vite 8.3.4
- **Styling**: Tailwind CSS 3.4.0
- **Icons**: Lucide React
- **3D Graphics**: Three.js, React Three Fiber, React Three Drei
- **Package Manager**: npm

## 📋 Prerequisites

Before you begin, ensure you have the following installed:
- Node.js (v18 or higher)
- npm (comes with Node.js)

## 🛠️ Installation

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd Auto-Mold-Hardness-Checking/frontend
   ```

2. **Install dependencies**
   ```bash
   npm install
   ```

3. **Start the development server**
   ```bash
   npm run dev
   ```

4. **Open your browser**
   Navigate to `http://localhost:5173` to view the application.

## 📁 Project Structure

```
frontend/
├── public/                 # Static assets
│   ├── favicon.png        # Website favicon
│   ├── bg-video.mp4       # Background video
│   ├── robotic_arm.glb    # 3D model file
│   └── logo.png           # Application logo
├── src/
│   ├── components/        # React components
│   │   ├── Hero.jsx       # Main hero section
│   │   ├── Navbar.jsx     # Navigation bar
│   │   └── Features.jsx   # Feature cards
│   ├── App.jsx            # Main application component
│   ├── main.jsx           # Application entry point
│   └── index.css          # Global styles
├── index.html             # HTML template
├── tailwind.config.js     # Tailwind configuration
├── vite.config.js         # Vite configuration
└── package.json           # Project dependencies
```

## 🎨 Customization

### Components

- **Hero**: Main landing section with CTA buttons and video showcase
- **Navbar**: Fixed navigation with logo and menu items
- **Features**: Feature cards highlighting platform capabilities

## 🏗️ Build for Production

To create an optimized production build:

```bash
npm run build
```

The built files will be in the `dist/` directory.

To preview the production build locally:

```bash
npm run preview
```

## 🔧 Available Scripts

- `npm run dev` - Start development server with hot reload
- `npm run build` - Build for production
- `npm run preview` - Preview production build locally
- `npm run lint` - Run ESLint for code linting

## 🌐 Browser Support

- Chrome (latest)
- Firefox (latest)
- Safari (latest)
- Edge (latest)

## 🤝 Contributing

Contributions are welcome! Please follow these steps:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## 📄 License

This project is proprietary software. All rights reserved.

## 👥 Contact

For support or inquiries, please contact the development team.
