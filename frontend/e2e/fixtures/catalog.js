import { officialSource } from '../../src/pages/rooms.js'
export const officialRooms = [
  {
    id: 'classic',
    name: 'Classic Room',
    category: 'room',
    size: 57,
    beds: 'choice',
    river: false,
    images: ['classic', 'classic-bathroom'],
    es: 'Materiales cálidos y detalles de inspiración china para descansar sobre la ciudad.',
    en: 'Warm materials and Chinese-inspired details for a retreat above the city.',
  },
  {
    id: 'classic-river',
    name: 'Classic Room River View',
    category: 'room',
    size: 57,
    beds: 'choice',
    river: true,
    images: ['classic-river'],
    es: 'Una perspectiva del río Jialing, con tonos cálidos y un interior sereno.',
    en: 'A perspective on the Jialing River, with warm tones and serene interiors.',
  },
  {
    id: 'club',
    name: 'Regent Club Room',
    category: 'room',
    size: 60,
    beds: 'king',
    river: false,
    images: ['club'],
    es: 'Espacio para bajar el ritmo, con vistas urbanas y acceso al Regent Club según la oferta oficial.',
    en: 'Room to slow down, with city views and Regent Club access as described by the hotel.',
  },
  {
    id: 'club-river',
    name: 'Regent Club Room River View',
    category: 'room',
    size: 60,
    beds: 'choice',
    river: true,
    images: ['club-river'],
    es: 'Luz natural y vistas al río desde un espacio contemporáneo de inspiración local.',
    en: 'Natural light and river views from a contemporary, locally inspired space.',
  },
  {
    id: 'junior',
    name: 'Junior Suite',
    category: 'suite',
    size: 83,
    beds: 'king',
    river: true,
    images: ['junior-living', 'junior'],
    es: 'Un refugio residencial donde se encuentran ciudad, cielo y río.',
    en: 'A residential retreat where city, sky and river come together.',
  },
  {
    id: 'executive',
    name: 'Executive Suite',
    category: 'suite',
    size: 117,
    beds: 'king',
    river: false,
    images: ['executive', 'executive-living'],
    es: 'Una suite amplia con detalles artesanales y espacios diferenciados para descansar.',
    en: 'A spacious suite with crafted details and distinct spaces to unwind.',
  },
  {
    id: 'regent',
    name: 'Regent Suite',
    category: 'suite',
    size: 215,
    beds: 'king',
    river: false,
    images: ['regent', 'regent-living'],
    es: 'La propuesta más amplia de esta selección: espacios residenciales en la planta superior.',
    en: 'The most expansive option in this selection: residential spaces on the top floor.',
  },
]

export const catalogItems = officialRooms.map((room, index) => ({
  id: `41000000-0000-0000-0000-${String(index + 1).padStart(12, '0')}`,
  slug: room.id,
  nameEs: room.name,
  nameEn: room.name,
  descriptionEs: room.es,
  descriptionEn: room.en,
  category: room.category,
  beds: room.beds,
  area: room.size,
  riverView: room.river,
  capacity: index < 4 ? 3 : 4,
  maxAdults: index < 4 ? 2 : 3,
  maxChildren: 2,
  nightlyRate: [880, 1080, 1280, 1480, 1880, 2680, 4880][index],
  active: true,
  activeRooms: 2,
  totalRooms: 2,
  sortOrder: index + 1,
  revision: 1,
  currency: 'CNY',
  isDemo: true,
  images: room.images.map((image, photo) => ({
    url: `/images/regent/${image}.webp`,
    altEs: `${room.name} · fotografía ${photo + 1}`,
    altEn: `${room.name} · photograph ${photo + 1}`,
    sortOrder: photo,
    sourceUrl: officialSource,
    credit: 'Regent / IHG',
  })),
}))
export const hotelFixture = {
  id: '40000000-0000-0000-0000-000000000001',
  name: 'RCC · Regent Chongqing',
  descriptionEs: 'Hotel de demostración',
  descriptionEn: 'Demonstration hotel',
  address: 'Chongqing, China',
  latitude: 29.57,
  longitude: 106.57,
  timeZone: 'Asia/Shanghai',
  revision: 1,
  services: [{ nameEs: 'Gastronomía', nameEn: 'Dining' }],
  isDemo: true,
}
export async function mockCatalogue(page) {
  await page.route('**/api/hotel', (route) => route.fulfill({ json: hotelFixture }))
  await page.route(/\/api\/room-types(?:\?.*)?$/, (route) => {
    const params = new URL(route.request().url()).searchParams
    const query = (params.get('search') ?? '').toLowerCase()
    const rows = catalogItems.filter(
      (item) =>
        (!params.get('category') || item.category === params.get('category')) &&
        (!params.has('riverView') || item.riverView) &&
        (item.nameEs.toLowerCase().includes(query) || item.nameEn.toLowerCase().includes(query)),
    )
    const current = Number(params.get('page') ?? 1),
      pageSize = Number(params.get('pageSize') ?? 20)
    return route.fulfill({
      json: {
        items: rows.slice((current - 1) * pageSize, current * pageSize),
        total: rows.length,
        page: current,
        pageSize,
        totalPages: Math.ceil(rows.length / pageSize),
      },
    })
  })
}
