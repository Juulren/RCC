export default function CityHero({ className, children, decoration }) {
  return (
    <section className={`city-page-hero subpage-hero ${className}`}>
      <img
        className="city-hero-photo"
        src="/images/chongqing-night.webp"
        alt=""
        fetchPriority="high"
      />
      <div className="city-hero-shade" aria-hidden="true" />
      <div className="shell city-hero-content">{children}</div>
      {decoration}
    </section>
  )
}
