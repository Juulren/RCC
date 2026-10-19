import CityBackdrop from './CityBackdrop.jsx'

export default function CityHero({ className, children, decoration, language }) {
  return (
    <section className={`city-page-hero subpage-hero ${className}`}>
      <CityBackdrop language={language} />
      <div className="shell city-hero-content">{children}</div>
      {decoration}
    </section>
  )
}
