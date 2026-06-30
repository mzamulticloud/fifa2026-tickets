import React from 'react';
import { Navbar } from './Navbar';
import { Footer } from './Footer';
import { Chatbot } from '@/components/Chatbot';

interface LayoutProps {
  children: React.ReactNode;
}

const Layout: React.FC<LayoutProps> = ({ children }) => {
  return (
    <div className="min-h-screen flex flex-col">
      <Navbar />
      <main className="flex-1 pt-16 md:pt-20">
        {children}
      </main>
      <Footer />
      {/* Story 2.5 / F5 — assistente conversacional flutuante (AC-7). */}
      <Chatbot />
    </div>
  );
};

export default Layout;
