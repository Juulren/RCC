import { motion, useReducedMotion } from 'motion/react'

const tags = { div: motion.div, section: motion.section }

export default function Reveal({ as = 'div', delay = 0, children, ...props }) {
  const reduced = useReducedMotion()
  const animated = !reduced && typeof window.IntersectionObserver !== 'undefined'
  if (!animated) {
    const StaticElement = as
    return <StaticElement {...props}>{children}</StaticElement>
  }
  const Element = tags[as]
  return (
    <Element
      initial={animated ? { opacity: 0.2, y: 38, filter: 'blur(5px)' } : false}
      whileInView={{ opacity: 1, y: 0, filter: 'blur(0px)' }}
      viewport={{ once: true, amount: 0.12 }}
      transition={{ duration: animated ? 0.7 : 0, delay, ease: [0.22, 1, 0.36, 1] }}
      {...props}
    >
      {children}
    </Element>
  )
}
